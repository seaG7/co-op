using System.Collections.Generic;
using UnityEngine;
using Data.Configs;
using Gameplay.Player.Vitals;
using Gameplay.World.Enemies.AI;

namespace Infrastructure.Services.Enemies
{
    public sealed class EnemyTargetingService : IEnemyTargetingService
    {
        private readonly List<Transform> _cannons = new();

        public void RegisterCannon(Transform attach) { if (attach != null && !_cannons.Contains(attach)) _cannons.Add(attach); }
        public void UnregisterCannon(Transform attach) { _cannons.Remove(attach); }

        public EnemyTarget Resolve(Vector3 enemyPos, EnemyConfig cfg, EnemyTarget current)
        {
            Transform cannon = NearestCannon(enemyPos);
            Vector3 cannonPos = cannon != null ? cannon.position : enemyPos;

            PlayerVitals blocker = null;
            float bestSq = float.MaxValue;
            float threatSq = cfg.PlayerThreatRadius * cfg.PlayerThreatRadius;
            var players = PlayerVitals.All;
            for (int i = 0; i < players.Count; i++)
            {
                var v = players[i];
                if (v == null || !v.IsAlive) continue;
                float sq = (v.transform.position - enemyPos).sqrMagnitude;
                bool close = sq <= threatSq;
                bool blocking = TargetingMath.IsBlockingPlayer(enemyPos, v.transform.position, cannonPos, cfg.PlayerAggroRadius, cfg.BlockingAngleDeg);
                if (!close && !blocking) continue;
                if (sq < bestSq) { bestSq = sq; blocker = v; }
            }
            if (blocker != null)
                return new EnemyTarget { Kind = EnemyTargetKind.Player, Transform = blocker.transform, Player = blocker };

            if (cannon != null)
                return new EnemyTarget { Kind = EnemyTargetKind.Cannon, Transform = cannon };

            var nearest = NearestPlayer(enemyPos);
            if (nearest != null)
                return new EnemyTarget { Kind = EnemyTargetKind.Player, Transform = nearest.transform, Player = nearest };

            return current;
        }

        private Transform NearestCannon(Vector3 pos)
        {
            Transform best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < _cannons.Count; i++)
            {
                if (_cannons[i] == null) continue;
                float sq = (_cannons[i].position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = _cannons[i]; }
            }
            return best;
        }

        private static PlayerVitals NearestPlayer(Vector3 pos)
        {
            PlayerVitals best = null;
            float bestSq = float.MaxValue;
            var players = PlayerVitals.All;
            for (int i = 0; i < players.Count; i++)
            {
                var v = players[i];
                if (v == null || !v.IsAlive) continue;
                float sq = (v.transform.position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = v; }
            }
            return best;
        }
    }
}
