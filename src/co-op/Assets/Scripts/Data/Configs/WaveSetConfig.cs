using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Wave Set Config", fileName = "WaveSetConfig")]
    public sealed class WaveSetConfig : ScriptableObject
    {
        [Tooltip("Seconds after the cannon is fully assembled before the source opens and enemies start spawning.")]
        [Min(0f)] public float PostAssembleDelaySec = 5f;

        [Tooltip("Seconds between enemy spawns while the source is Open.")]
        public float SpawnInterval = 2.5f;

        [Tooltip("Max simultaneously-alive enemies (0 = unlimited).")]
        [Min(0)] public int MaxAliveEnemies = 15;

        [Tooltip("Charged cannon shots needed to destroy the source. Model B: the source is vulnerable for the whole wave; the real gate is the cannon's corpse charge, not a timed window.")]
        [Min(1)] public int HitsToDestroy = 1;

        [Tooltip("Random horizontal radius (m) scattered around the spawn point so a burst doesn't stack on one spot (stacked spiders climb each other and jam). 0 = exact point.")]
        [Min(0f)] public float SpawnScatterRadius = 1.5f;
    }
}
