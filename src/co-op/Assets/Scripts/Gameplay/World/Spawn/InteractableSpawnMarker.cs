using Data.Configs;
using UnityEngine;

namespace Gameplay.World.Spawn
{
    [DisallowMultipleComponent]
    public sealed class InteractableSpawnMarker : MonoBehaviour
    {
        [Tooltip("Item type that may spawn here. The marker pulls its visual preview from Config.Prefab.")]
        public InteractableItemConfig Config;

        [Tooltip("Per-marker spawn probability. 1 = always spawns, 0 = never.")]
        [Range(0f, 1f)] public float SpawnChance = 1f;

#if UNITY_EDITOR
        [Header("Editor preview")]
        [Tooltip("Tint applied to the ghost mesh in the Scene view.")]
        public Color GhostTint = new Color(0.4f, 1f, 0.4f, 0.5f);
        [Tooltip("Also draw a wireframe outline on top of the solid ghost.")]
        public bool DrawWireframe = true;

        private void OnDrawGizmos()
        {
            DrawPrefabGhost(selected: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawPrefabGhost(selected: true);
        }

        private void DrawPrefabGhost(bool selected)
        {
            Gizmos.color = selected ? Color.yellow : new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.15f);

            if (Config == null || Config.Prefab == null) return;

            var prefabRoot = Config.Prefab.transform;
            var meshFilters = Config.Prefab.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            if (meshFilters == null || meshFilters.Length == 0) return;

            var prevMatrix = Gizmos.matrix;
            var prevColor = Gizmos.color;

            foreach (var mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;

                Matrix4x4 localToPrefab = prefabRoot.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Gizmos.matrix = transform.localToWorldMatrix * localToPrefab;

                Gizmos.color = GhostTint;
                Gizmos.DrawMesh(mf.sharedMesh, 0);

                if (DrawWireframe)
                {
                    Gizmos.color = new Color(GhostTint.r * 0.5f, GhostTint.g * 0.9f, GhostTint.b * 0.5f, 1f);
                    Gizmos.DrawWireMesh(mf.sharedMesh, 0);
                }
            }

            Gizmos.matrix = prevMatrix;
            Gizmos.color = prevColor;
        }
#endif
    }
}
