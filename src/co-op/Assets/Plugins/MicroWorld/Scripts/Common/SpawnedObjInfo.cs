using UnityEngine;

namespace MicroWorldNS.Spawners
{
    /// <summary>
    /// Info about spawned object
    /// </summary>
    public class SpawnedObjInfo : MonoBehaviour
    {
        public float OffsetY;
        public string SourceSpawnerName;
        public string SourceTerrainExclusiveGroup;
        public string SourceSemantic;
        public SpawnedObjectOptimizationPolicy OptimizationPolicy;
    }
}
