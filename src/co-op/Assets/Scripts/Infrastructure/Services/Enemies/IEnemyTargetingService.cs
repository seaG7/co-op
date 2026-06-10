using UnityEngine;
using Data.Configs;
using Gameplay.World.Enemies.AI;

namespace Infrastructure.Services.Enemies
{
    public interface IEnemyTargetingService
    {
        void RegisterCannon(Transform attach);
        void UnregisterCannon(Transform attach);
        EnemyTarget Resolve(Vector3 enemyPos, EnemyConfig cfg, EnemyTarget current);
    }
}
