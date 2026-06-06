using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Configs;
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
        [SerializeField] private Transform[] _spawnPoints;

        [Inject] private INetworkSpawnService _spawner;
        [Inject] private INetworkService _network;
        [Inject] private SignalBus _signalBus;

        public readonly SyncVar<float> Health = new(0f);
        public readonly SyncVar<bool> IsVulnerable = new(false);
        public readonly SyncVar<bool> Destroyed = new(false);

        public float MaxHealth => _waveSet != null ? _waveSet.SourceMaxHealth : 100f;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
            IsVulnerable.OnChange += OnVulnerableChanged;
            Health.OnChange += OnHealthChanged;
        }

        public override void OnStopNetwork()
        {
            IsVulnerable.OnChange -= OnVulnerableChanged;
            Health.OnChange -= OnHealthChanged;
            _all.Remove(this);
            base.OnStopNetwork();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Health.Value = MaxHealth;
            if (_waveSet == null || _waveSet.EnemyPrefab == null)
            {
                Debug.LogWarning("[Source] No WaveSetConfig or EnemyPrefab assigned; no waves will spawn.", this);
                return;
            }
            RunWavesAsync().Forget();
        }

        public void ServerApplyDamage(float amount)
        {
            if (!IsServerInitialized || Destroyed.Value || !IsVulnerable.Value) return;
            Health.Value = Mathf.Max(0f, Health.Value - amount);
            if (Health.Value <= 0f) ServerDestroy();
        }

        private void ServerDestroy()
        {
            if (Destroyed.Value) return;
            Destroyed.Value = true;
            IsVulnerable.Value = false;
            Enemy.ServerDespawnAll();
            _signalBus?.Fire(new SourceDestroyedSignal());
        }

        private async UniTaskVoid RunWavesAsync()
        {
            var ct = this.GetCancellationTokenOnDestroy();
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, _waveSet.GraceBeforeFirstWave)), cancellationToken: ct);

                for (int i = 0; i < _waveSet.Waves.Count; i++)
                {
                    if (Destroyed.Value || _network == null || !_network.IsServer) return;

                    var wave = _waveSet.Waves[i];
                    _signalBus?.Fire(new WaveStartedSignal(i));

                    int count = Mathf.Max(1, wave.Count);
                    for (int n = 0; n < count; n++)
                    {
                        if (Destroyed.Value) return;
                        SpawnOne();
                        if (wave.SpawnInterval > 0f)
                            await UniTask.Delay(TimeSpan.FromSeconds(wave.SpawnInterval), cancellationToken: ct);
                    }

                    if (Destroyed.Value) return;
                    _signalBus?.Fire(new WaveClearedSignal(i));

                    if (i < _waveSet.Waves.Count - 1)
                    {
                        IsVulnerable.Value = true;
                        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, _waveSet.PauseBetweenWaves)), cancellationToken: ct);
                        IsVulnerable.Value = false;
                        if (Destroyed.Value) return;
                    }
                }

                _signalBus?.Fire(new AllWavesClearedSignal());
            }
            catch (OperationCanceledException) { }
        }

        private void SpawnOne()
        {
            Vector3 pos = _spawnPoints != null && _spawnPoints.Length > 0 && _spawnPoints[0] != null
                ? _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)].position
                : transform.position;
            _spawner.SpawnNetworked(_waveSet.EnemyPrefab, pos, Quaternion.identity, owner: null);
        }

        private void OnVulnerableChanged(bool prev, bool next, bool asServer)
        {
            if (asServer) return;
            _signalBus?.Fire(new SourceVulnerableSignal(next));
        }

        private void OnHealthChanged(float prev, float next, bool asServer)
        {
            if (asServer) return;
            _signalBus?.Fire(new SourceDamagedSignal(next, MaxHealth));
        }
    }
}
