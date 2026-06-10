#if UNITY_EDITOR
using System.Collections.Generic;
using Gameplay.World.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EditorTools
{
    [InitializeOnLoad]
    public static class CannonGhostPreview
    {
        private static readonly Dictionary<Renderer, Material[]> _saved = new();

        static CannonGhostPreview()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingEditMode) Restore();
            };
        }

        [MenuItem("Tools/CoOp/Cannon/Toggle Ghost Preview %#g", false, 200)]
        private static void Toggle()
        {
            if (_saved.Count > 0) { Restore(); return; }
            Apply();
        }

        private static void Apply()
        {
            Restore();
            var slots = FindSlots();
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                var so = new SerializedObject(slot);
                var ghost = so.FindProperty("_ghostMaterial")?.objectReferenceValue as Material;
                if (ghost == null) continue;

                foreach (var r in slot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || _saved.ContainsKey(r)) continue;
                    var originals = r.sharedMaterials;
                    _saved[r] = originals;
                    var ghosts = new Material[originals.Length];
                    for (int i = 0; i < ghosts.Length; i++) ghosts[i] = ghost;
                    r.sharedMaterials = ghosts;
                }
            }

            SceneView.RepaintAll();
            if (_saved.Count == 0)
                Debug.LogWarning("[CannonGhostPreview] No WeaponModuleSlot with a _ghostMaterial found. Assign ghost materials first.");
            else
                Debug.Log("[CannonGhostPreview] Ghost preview ON (Ctrl/Cmd+Shift+G to restore). Auto-restores on Play — Restore before saving the prefab.");
        }

        private static void Restore()
        {
            foreach (var kv in _saved)
                if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            _saved.Clear();
            SceneView.RepaintAll();
        }

        private static WeaponModuleSlot[] FindSlots()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                return stage.prefabContentsRoot.GetComponentsInChildren<WeaponModuleSlot>(true);
            return Object.FindObjectsByType<WeaponModuleSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
    }
}
#endif
