using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data.Configs;
using Data.World;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Gameplay.World.Enemies;
using Infrastructure.Services.Network;
using Infrastructure.Services.Spawn;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Sources
{
    public sealed class Source : InjectableNetworkBehaviour
    {
        private static readonly List<Source> _all = new();
        public static IReadOnlyList<Source> All => _all;

        [SerializeField] private WaveSetConfig _waveSet;
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private GameObject _portalPrefab;
        [SerializeField] private Transform[] _spawnPoints;

        [Tooltip("Layers that block a spawn point (Source, players, walls). EXCLUDE the Enemy layer so mobs don't block each other's spawns.")]
        [SerializeField] private LayerMask _spawnBlockMask = ~0;
        [Tooltip("Body radius (m) used to test that a spawn point is clear of obstacles.")]
        [SerializeField] private float _spawnCheckRadius = 0.5f;
        [Tooltip("Ring step (m) used when searching outward for a clear spawn point.")]
        [SerializeField] private float _spawnSearchStep = 1.5f;

        [Inject] private INetworkSpawnService _spawner;
        [Inject] private INetworkService _network;
        [Inject] private SignalBus _signalBus;

        public readonly SyncVar<SourceState> State = new(SourceState.Gather);
        public readonly SyncVar<float> OpenAmount = new(0f);
        public readonly SyncVar<bool> IsVulnerable = new(false);
        public readonly SyncVar<bool> Destroyed = new(false);

        private int _hits;
        private CancellationTokenSource _cts;
        [System.NonSerialized] public bool DebugSpawnsPaused;

        private float GatherDuration => _waveSet != null ? Mathf.Max(0f, _waveSet.GatherDurationSec) : 30f;
        private float SpawnInterval => _waveSet != null ? Mathf.Max(0.1f, _waveSet.SpawnInterval) : 2.5f;
        private int MaxAlive => _waveSet != null ? _waveSet.MaxAliveEnemies : 15;
        private int HitsToDestroy => _waveSet != null ? Mathf.Max(1, _waveSet.HitsToDestroy) : 1;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
            State.OnChange += OnStateChanged;
        }

        public override void OnStopNetwork()
        {
            State.OnChange -= OnStateChanged;
            _all.Remove(this);
            base.OnStopNetwork();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_waveSet == null || _enemyPrefab == null)
            {
                Debug.LogWarning("[Source] No WaveSetConfig or _enemyPrefab assigned; source will not run.", this);
                return;
            }
            _cts = new CancellationTokenSource();
            RunLifecycleAsync(_cts.Token).Forget();
        }

        public override void OnStopServer()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            base.OnStopServer();
        }

        public void ServerApplyDamage(float amount)
        {
            if (!IsServerInitialized || Destroyed.Value || State.Value != SourceState.Open) return;
            _hits++;
            _signalBus?.Fire(new SourceDamagedSignal(Mathf.Max(0, HitsToDestroy - _hits), HitsToDestroy));
            if (_hits >= HitsToDestroy) ServerDestroy();
        }

        private void ServerDestroy()
        {
            if (Destroyed.Value) return;
            Destroyed.Value = true;
            ApplyStateVars(SourceState.Destroyed);
            RpcAnnounce(SourceState.Destroyed, 0f, 0f);
            _cts?.Cancel();
            Enemy.ServerDespawnAll();
            SpawnPortal();
            _signalBus?.Fire(new SourceDestroyedSignal());
        }

        private void SpawnPortal()
        {
            if (_portalPrefab == null)
            {
                Debug.LogWarning("[Source] No _portalPrefab assigned; round cannot complete.", this);
                return;
            }
            _spawner.SpawnNetworked(_portalPrefab, transform.position, Quaternion.identity, owner: null);
        }

        private async UniTaskVoid RunLifecycleAsync(CancellationToken ct)
        {
            try
            {
                ApplyStateVars(SourceState.Gather);
                await TimedPhaseAsync(SourceState.Gather, GatherDuration, ct);
                if (Destroyed.Value) return;

                ApplyStateVars(SourceState.Open);
                RpcAnnounce(SourceState.Open, 0f, 0f);

                float spawnT = 0f;
                while (!Destroyed.Value && _network != null && _network.IsServer)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    spawnT -= Time.deltaTime;
                    if (spawnT <= 0f)
                    {
                        if (!Destroyed.Value && !DebugSpawnsPaused && (MaxAlive <= 0 || Enemy.All.Count < MaxAlive))
                            SpawnOne();
                        spawnT = SpawnInterval;
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private async UniTask TimedPhaseAsync(SourceState state, float total, CancellationToken ct)
        {
            float left = total;
            while (left > 0f && !Destroyed.Value)
            {
                RpcAnnounce(state, left, total);
                float step = Mathf.Min(0.5f, left);
                await UniTask.Delay(TimeSpan.FromSeconds(step), cancellationToken: ct);
                left -= step;
            }
        }

        private float SpawnScatter => _waveSet != null ? Mathf.Max(0f, _waveSet.SpawnScatterRadius) : 1.5f;

        private void SpawnOne()
        {
            Vector3 pos = _spawnPoints != null && _spawnPoints.Length > 0 && _spawnPoints[0] != null
                ? _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)].position
                : transform.position;
            float r = SpawnScatter;
            if (r > 0f)
            {
                Vector2 o = UnityEngine.Random.insideUnitCircle * r;
                pos += new Vector3(o.x, 0f, o.y);
            }
            pos = FindClearSpawn(pos);
            _spawner.SpawnNetworked(_enemyPrefab, pos, Quaternion.identity, owner: null);
        }

        private Vector3 FindClearSpawn(Vector3 pos)
        {
            if (IsClear(pos)) return pos;
            float step = Mathf.Max(0.5f, _spawnSearchStep);
            for (int ring = 1; ring <= 4; ring++)
            {
                float rad = ring * step;
                for (int a = 0; a < 8; a++)
                {
                    float ang = (a / 8f) * Mathf.PI * 2f;
                    Vector3 p = pos + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                    if (IsClear(p)) return p;
                }
            }
            return pos;
        }

        private bool IsClear(Vector3 pos)
            => Physics.OverlapSphere(pos, _spawnCheckRadius, _spawnBlockMask, QueryTriggerInteraction.Ignore).Length == 0;

        public void ServerDebugSpawnOne() { if (IsServerInitialized) SpawnOne(); }
        public void ServerDebugDespawnEnemies() { if (IsServerInitialized) Enemy.ServerDespawnAll(); }
        public void ServerDebugDestroy() { if (IsServerInitialized) ServerDestroy(); }

        private void ApplyStateVars(SourceState state)
        {
            State.Value = state;
            OpenAmount.Value = state == SourceState.Open ? 1f : 0f;
            IsVulnerable.Value = state == SourceState.Open;
        }

        [ObserversRpc]
        private void RpcAnnounce(SourceState state, float remaining, float total)
            => _signalBus?.Fire(new SourceStateChangedSignal(state, remaining, total));

        private void OnStateChanged(SourceState prev, SourceState next, bool asServer)
        {
            if (asServer) return;
            if (next == SourceState.Open) _signalBus?.Fire(new SourceVulnerableSignal(true));
            else if (prev == SourceState.Open) _signalBus?.Fire(new SourceVulnerableSignal(false));
        }
    }
}
