using System.Collections.Generic;
using Data.Configs;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Data.Effects;
using FishNet.Object;
using Gameplay.Player.Vitals;
using Gameplay.World.Enemies.AI;
using Infrastructure.Services.Effects;
using Infrastructure.Services.Enemies;
using Infrastructure.Services.Spawn;
using Gameplay.World.Weapon;
using MimicSpace;
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
        [SerializeField] private GameObject _corpsePrefab;

        [Inject] private IEnemyTargetingService _targeting;
        [Inject] private SignalBus _signalBus;
        [Inject] private ISfxService _sfx;
        [Inject] private INetworkSpawnService _spawner;

        public readonly SyncVar<float> Health = new(0f);

        public Vector3 HitCenter => transform.position;

        private EnemyBrain _brain;
        private ISfxHandle _moveLoop;
        private Vector3 _lastStepPos;
        private bool _stepInit;

        private readonly SyncVar<int> _latchPlayerId = new(-1);
        private readonly SyncVar<int> _latchModuleOrder = new(-1);
        private Mimic _mimic;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
            _latchPlayerId.OnChange += OnLatchChanged;
            _latchModuleOrder.OnChange += OnLatchChanged;
        }

        public override void OnStopNetwork()
        {
            _latchPlayerId.OnChange -= OnLatchChanged;
            _latchModuleOrder.OnChange -= OnLatchChanged;
            if (_mimic != null) _mimic.ClearCling();
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
            _brain = new EnemyBrain(transform, _config, _targeting);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _signalBus?.Fire(new EnemySpawnedSignal(transform.position));
            _moveLoop = _sfx?.PlayLoop(SfxId.EnemyMove, transform);
            _lastStepPos = transform.position;
            _stepInit = true;
            _mimic = GetComponentInChildren<Mimic>(true);
            RefreshCling();
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

            int latchId = ctx.Latch.Active && ctx.Latch.Player != null ? ctx.Latch.Player.OwnerId : -1;
            if (_latchPlayerId.Value != latchId) _latchPlayerId.Value = latchId;
            int latchMod = ctx.Latch.Active && ctx.Latch.Module != null ? ctx.Latch.Module.Order : -1;
            if (_latchModuleOrder.Value != latchMod) _latchModuleOrder.Value = latchMod;
        }

        private void LateUpdate()
        {
            if (!_stepInit || _sfx == null) return;
            Vector3 d = transform.position - _lastStepPos; d.y = 0f;
            if (d.sqrMagnitude < 1.3f * 1.3f) return;
            _lastStepPos = transform.position;
            _sfx.Play(SfxId.EnemyStep, transform.position);
        }

        private void OnLatchChanged(int prev, int next, bool asServer) => RefreshCling();

        private static readonly HumanBodyBones[] _clingBones =
        {
            HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.Head,
            HumanBodyBones.Chest, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperArm, HumanBodyBones.Hips
        };

        // Re-evaluates what the legs grip: a cannon module's meshes (priority) or a latched player's
        // body bones. Runs on every peer from the synced latch ids.
        private void RefreshCling()
        {
            if (_mimic == null) return;

            int mod = _latchModuleOrder.Value;
            if (mod >= 0)
            {
                var slot = WeaponModuleSlot.Find(mod);
                if (slot != null) { _mimic.SetCling(slot.ClingTargets()); return; }
            }

            int playerOwnerId = _latchPlayerId.Value;
            if (playerOwnerId >= 0)
            {
                PlayerVitals target = null;
                var players = PlayerVitals.All;
                for (int i = 0; i < players.Count; i++)
                    if (players[i] != null && players[i].OwnerId == playerOwnerId) { target = players[i]; break; }

                if (target != null)
                {
                    var bones = new List<Transform>();
                    var anim = target.GetComponentInChildren<Animator>();
                    if (anim != null && anim.isHuman)
                        for (int i = 0; i < _clingBones.Length; i++)
                        {
                            var b = anim.GetBoneTransform(_clingBones[i]);
                            if (b != null) bones.Add(b);
                        }
                    if (bones.Count == 0) bones.Add(target.transform);
                    _mimic.SetCling(bones);
                    return;
                }
            }

            _mimic.ClearCling();
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
