using UnityEngine;

namespace MicroWorldNS.Spawners
{
    public static class SpawnedObjectMetadataUtility
    {
        public static void Apply(
            GameObject spawnedObject,
            Transform spawnerTransform,
            string sourceSpawnerName,
            string sourceTerrainExclusiveGroup,
            string sourceSemantic)
        {
            if (spawnedObject == null || !spawnedObject.TryGetComponent(out SpawnedObjInfo info))
                return;

            info.SourceSpawnerName = sourceSpawnerName ?? string.Empty;
            info.SourceTerrainExclusiveGroup = sourceTerrainExclusiveGroup ?? string.Empty;
            info.SourceSemantic = sourceSemantic ?? string.Empty;
            info.OptimizationPolicy = ResolvePolicy(spawnedObject, spawnerTransform);
        }

        private static SpawnedObjectOptimizationPolicy ResolvePolicy(GameObject spawnedObject, Transform spawnerTransform)
        {
            if (spawnedObject.TryGetComponent(out SpawnedObjectOptimizationAuthoring spawnedAuthoring) &&
                spawnedAuthoring.Policy != SpawnedObjectOptimizationPolicy.Default)
            {
                return spawnedAuthoring.Policy;
            }

            Transform cursor = spawnerTransform;
            while (cursor != null)
            {
                if (cursor.TryGetComponent(out SpawnedObjectOptimizationAuthoring authoring) &&
                    authoring.Policy != SpawnedObjectOptimizationPolicy.Default)
                {
                    return authoring.Policy;
                }

                cursor = cursor.parent;
            }

            return SpawnedObjectOptimizationPolicy.Default;
        }
    }
}
