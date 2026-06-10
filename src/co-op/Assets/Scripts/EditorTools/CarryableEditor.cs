#if UNITY_EDITOR
using System.Collections.Generic;
using Data.Configs;
using Gameplay.World.Items;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    [CustomEditor(typeof(Carryable))]
    public sealed class CarryableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var carryable = (Carryable)target;
            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Carry tuning", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Tune pose / palms / elbows with Scene-view handles in Play Mode, then bake.", MessageType.Info);
                if (GUILayout.Button("Open Carry Tuner ▸", GUILayout.Height(24)))
                    CoOp.EditorTools.CarryTunerWindow.Open();

                if (targets.Length == 1 && GUILayout.Button("Bake this instance's pose + grips → prefab", GUILayout.Height(22)))
                    Bake(carryable);
            }
        }

        private static void Bake(Carryable carryable)
        {
            if (carryable == null) return;

            var so = new SerializedObject(carryable);
            var prefab = so.FindProperty("_config")?.objectReferenceValue is InteractableItemConfig cfg ? cfg.Prefab : null;
            if (prefab == null) prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(carryable.gameObject);

            string path = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Bake",
                    "No source prefab found. Assign InteractableItemConfig.Prefab on this Carryable so the tool knows which prefab to write to.",
                    "OK");
                return;
            }

            Transform root = carryable.transform;
            var grips = new List<(string path, Vector3 pos, Quaternion rot, Vector3 scale)>();

            void Capture(Transform t)
            {
                if (t == null || t == root) return;
                string rel = RelativePath(root, t);
                if (string.IsNullOrEmpty(rel)) return;
                grips.Add((rel, t.localPosition, t.localRotation, t.localScale));
            }

            Capture(so.FindProperty("_leftHandGrip")?.objectReferenceValue as Transform);
            Capture(so.FindProperty("_rightHandGrip")?.objectReferenceValue as Transform);
            var anchors = so.FindProperty("_grabAnchors");
            if (anchors != null && anchors.isArray)
                for (int i = 0; i < anchors.arraySize; i++)
                    Capture(anchors.GetArrayElementAtIndex(i).objectReferenceValue as Transform);

            Vector3 holdPos = carryable.HoldPositionOffset;
            Vector3 holdEuler = carryable.HoldEulerOffset;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            int applied = 0;
            try
            {
                var pc = contents.GetComponentInChildren<Carryable>(true);
                Transform prefabRoot = pc != null ? pc.transform : contents.transform;

                if (pc != null)
                {
                    var pso = new SerializedObject(pc);
                    pso.FindProperty("_holdPositionOffset").vector3Value = holdPos;
                    pso.FindProperty("_holdEulerOffset").vector3Value = holdEuler;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                }

                foreach (var g in grips)
                {
                    var t = prefabRoot.Find(g.path);
                    if (t == null)
                    {
                        Debug.LogWarning($"[CarryableEditor] Socket '{g.path}' not found in prefab '{path}' — skipped.");
                        continue;
                    }
                    t.localPosition = g.pos;
                    t.localRotation = g.rot;
                    t.localScale = g.scale;
                    applied++;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            carryable.HoldTuning = false;
            Debug.Log($"[CarryableEditor] Baked in-hand pose + {applied}/{grips.Count} grip transform(s) into '{path}'.", prefab);
        }

        private static string RelativePath(Transform root, Transform t)
        {
            var parts = new List<string>();
            Transform cur = t;
            while (cur != null && cur != root)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            if (cur != root) return null;
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
#endif
