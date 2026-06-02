using UnityEngine;

namespace MicroWorldNS.Spawners
{
    public enum SpawnedObjectOptimizationPolicy : byte
    {
        Default = 0,
        Exclude = 1,
        MeshCombine = 2,
        ChunkCombine = 3
    }

    [DisallowMultipleComponent]
    public class SpawnedObjectOptimizationAuthoring : MonoBehaviour
    {
        [Tooltip("Default: no explicit policy. Exclude: never combine. MeshCombine: combine by hierarchy. ChunkCombine: combine by global cells.")]
        public SpawnedObjectOptimizationPolicy Policy = SpawnedObjectOptimizationPolicy.Default;
    }
}
