using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/World Gen Config", fileName = "WorldGenConfig")]
    public sealed class WorldGenConfig : ScriptableObject
    {
        [Header("Seed")]
        [Tooltip("If true, host uses FixedSeed every game (debug). If false, host picks a random non-zero seed.")]
        public bool UseFixedSeed = false;
        public int FixedSeed = 12345;

        [Header("Anchors")]
        [Tooltip("Number of component spawn anchors placed across the terrain.")]
        public int ComponentAnchorCount = 6;
        [Tooltip("Minimum world-space distance between Base and Source anchors (meters).")]
        public float MinBaseSourceDistance = 60f;
        [Tooltip("Minimum distance between any two component anchors (meters).")]
        public float MinComponentSpacing = 12f;
        [Tooltip("Player spawn slots arranged in a circle of this radius around the Base anchor (meters).")]
        public float BaseSpawnRadius = 3f;
        [Tooltip("Max slope (degrees) a placed anchor tolerates before re-sampling.")]
        [Range(0f, 89f)] public float MaxAnchorSlope = 25f;

        [Header("Components")]
        [Tooltip("Probability per ComponentSpawn anchor that an item will spawn on it.")]
        [Range(0f, 1f)]
        public float ComponentSpawnChance = 0.75f;
    }
}
