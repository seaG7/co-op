using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Enemy Config", fileName = "EnemyConfig")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [Tooltip("Move speed (m/s) while chasing.")]
        public float MoveSpeed = 3.5f;

        [Tooltip("Hit points.")]
        public float MaxHealth = 10f;

        [Tooltip("Stop distance from the target (m) so it doesn't jitter into the player.")]
        public float StopDistance = 1.1f;

        [Tooltip("Range (m) within which the enemy can knock a player down.")]
        public float AttackRange = 1.4f;

        [Tooltip("Seconds between knockdown attempts.")]
        public float AttackCooldown = 2f;
    }
}
