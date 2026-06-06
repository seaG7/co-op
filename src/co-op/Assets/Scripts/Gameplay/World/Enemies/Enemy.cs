using System.Collections.Generic;
using Data.Configs;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Gameplay.Player.Vitals;
using UnityEngine;

namespace Gameplay.World.Enemies
{
    public sealed class Enemy : InjectableNetworkBehaviour
    {
        private static readonly List<Enemy> _all = new();
        public static IReadOnlyList<Enemy> All => _all;

        [SerializeField] private EnemyConfig _config;

        public readonly SyncVar<float> Health = new(0f);

        private PlayerVitals _target;
        private float _retargetTimer;
        private float _attackTimer;

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
            Health.Value = _config != null ? _config.MaxHealth : 10f;
        }

        public void ServerDespawn()
        {
            if (IsServerInitialized && NetworkObject != null)
                ServerManager.Despawn(NetworkObject);
        }

        public static void ServerDespawnAll()
        {
            var snapshot = _all.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i]?.ServerDespawn();
        }

        public void ServerApplyDamage(float amount)
        {
            if (!IsServerInitialized) return;
            Health.Value -= amount;
            if (Health.Value <= 0f && NetworkObject != null)
                ServerManager.Despawn(NetworkObject);
        }

        private void Update()
        {
            if (!IsServerInitialized || _config == null) return;

            float dt = Time.deltaTime;
            if (_attackTimer > 0f) _attackTimer -= dt;

            _retargetTimer -= dt;
            if (_target == null || !_target.IsAlive || _retargetTimer <= 0f)
            {
                _target = FindNearestAlivePlayer();
                _retargetTimer = 0.5f;
            }
            if (_target == null) return;

            Vector3 to = _target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist > _config.StopDistance)
            {
                Vector3 dir = to / Mathf.Max(dist, 1e-4f);
                transform.position += dir * (_config.MoveSpeed * dt);
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }

            if (dist <= _config.AttackRange && _attackTimer <= 0f)
            {
                _target.ServerKnockDown();
                _attackTimer = _config.AttackCooldown;
            }
        }

        private PlayerVitals FindNearestAlivePlayer()
        {
            var all = PlayerVitals.All;
            PlayerVitals best = null;
            float bestSq = float.MaxValue;
            Vector3 p = transform.position;
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i];
                if (v == null || !v.IsAlive) continue;
                float sq = (v.transform.position - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = v; }
            }
            return best;
        }
    }
}
