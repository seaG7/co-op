using System.Collections.Generic;
using Data.Configs;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Data.Effects;
using FishNet.Object;
using Gameplay.World.Enemies.AI;
using Infrastructure.Services.Effects;
using Infrastructure.Services.Enemies;
using Infrastructure.Services.Spawn;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Enemies
{
    public sealed class Enemy : InjectableNetworkBehaviour
    {
        private static readonly List<Enemy> _all = new();
        public static IReadOnlyList<Enemy> All => _all;

        [SerializeField] private EnemyConfig _config;
        [SerializeField] private Transform _body;
        [SerializeField] private GameObject _corpsePrefab;

        [Inject] private IEnemyTargetingService _targeting;
        [Inject] private SignalBus _signalBus;
        [Inject] private ISfxService _sfx;
        [Inject] private INetworkSpawnService _spawner;

        public readonly SyncVar<float> Health = new(0f);

        private EnemyBrain _brain;
        private ISfxHandle _moveLoop;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
        }

        public override void OnStopNetwork()
        {
            _all.Remove(this);
            base.OnStopNetwork();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Health.Value = _config != null ? _config.MaxHealth : 30f;
            if (_config == null)
            {
                Debug.LogError($"[{nameof(Enemy)}] No EnemyConfig assigned.", this);
                return;
            }
            _brain = new EnemyBrain(_body != null ? _body : transform, _config, _targeting);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _signalBus?.Fire(new EnemySpawnedSignal(transform.position));
            _moveLoop = _sfx?.PlayLoop(SfxId.EnemyMove, transform);
        }

        public override void OnStopClient()
        {
            _moveLoop?.Stop();
            _moveLoop = null;
            _signalBus?.Fire(new EnemyDiedSignal(transform.position));
            base.OnStopClient();
        }

        private void Update()
        {
            if (!IsServerInitialized || _brain == null) return;
            _brain.Tick(Time.deltaTime);

            var ctx = _brain.Context;
            if (ctx.PendingKnockdown != null)
            {
                var player = ctx.PendingKnockdown;
                ctx.PendingKnockdown = null;
                player.HasLatchedAttacker = true;
                player.ServerKnockDown();
            }

            if (ctx.PendingEffect != EnemyEffectKind.None)
            {
                var pe = ctx.PendingEffect;
                ctx.PendingEffect = EnemyEffectKind.None;
                RpcEffect((byte)pe, transform.position, ctx.PendingLatchOnPlayer);
            }
        }

        public void ServerApplyDamage(float amount)
        {
            if (!IsServerInitialized) return;
            Health.Value -= amount;
            if (Health.Value <= 0f) { SpawnCorpse(); ServerDespawn(); return; }
            RpcEffect((byte)EnemyEffectKind.Damaged, transform.position, false);
        }

        private void SpawnCorpse()
        {
            if (_corpsePrefab != null && _spawner != null)
                _spawner.SpawnNetworked(_corpsePrefab, transform.position, transform.rotation, owner: null);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcEffect(byte kind, Vector3 pos, bool onPlayer)
        {
            if (_signalBus == null) return;
            switch ((EnemyEffectKind)kind)
            {
                case EnemyEffectKind.PrePounce: _signalBus.Fire(new EnemyPrePounceSignal(pos)); break;
                case EnemyEffectKind.Pounced: _signalBus.Fire(new EnemyPouncedSignal(pos)); break;
                case EnemyEffectKind.Latched: _signalBus.Fire(new EnemyLatchedSignal(pos, onPlayer)); break;
                case EnemyEffectKind.Damaged: _signalBus.Fire(new EnemyDamagedSignal(pos)); break;
            }
        }

        public void ServerDespawn()
        {
            if (!IsServerInitialized || NetworkObject == null) return;
            ReleaseLatchedPlayer();
            ServerManager.Despawn(NetworkObject);
        }

        public static void ServerDespawnAll()
        {
            var snapshot = _all.ToArray();
            for (int i = 0; i < snapshot.Length; i++) snapshot[i]?.ServerDespawn();
        }

        private void ReleaseLatchedPlayer()
        {
            var ctx = _brain?.Context;
            if (ctx == null || !ctx.Latch.Active) return;
            if (ctx.Latch.Player != null)
            {
                ctx.Latch.Player.HasLatchedAttacker = false;
                ctx.Latch.Player.ServerRevive();
            }
            if (ctx.Latch.Module != null) ctx.Latch.Module.RemoveMob();
        }
    }
}
