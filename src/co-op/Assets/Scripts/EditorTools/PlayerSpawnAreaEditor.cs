#if UNITY_EDITOR
using Gameplay.World.Spawn;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    [CustomEditor(typeof(PlayerSpawnArea))]
    public sealed class PlayerSpawnAreaEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var area = (PlayerSpawnArea)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Polygon", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply preset"))
                {
                    Undo.RecordObject(area, "Apply spawn-area preset");
                    area.ApplyPreset();
                    EditorUtility.SetDirty(area);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview ghost (random-fallback rotation)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!area.PreviewRotationIsDraft))
                {
                    if (GUILayout.Button("Apply player rotation (preview → spawn)"))
                    {
                        Undo.RecordObject(area, "Apply preview rotation");
                        area.ApplyPreviewRotation();
                        EditorUtility.SetDirty(area);
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("Reset preview to applied"))
                    {
                        Undo.RecordObject(area, "Reset preview rotation");
                        area.ResetPreviewRotation();
                        EditorUtility.SetDirty(area);
                        SceneView.RepaintAll();
                    }
                }
            }
            if (GUILayout.Button("Snap preview to area"))
            {
                Undo.RecordObject(area, "Snap preview to area");
                area.SnapPreviewToArea();
                EditorUtility.SetDirty(area);
                SceneView.RepaintAll();
            }

            if (area.PreviewRotationIsDraft)
            {
                EditorGUILayout.HelpBox(
                    $"Preview rotation differs from the baked spawn rotation. " +
                    $"Random-fallback yaw used at runtime: {area.SpawnEulerDeg.y:F0}°. " +
                    $"Click 'Apply player rotation' to bake the preview.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fixed spawn points (claim pool)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add fixed spawn point (at preview pos+rot)"))
                {
                    Undo.RecordObject(area, "Add fixed spawn point");
                    area.AddFixedSpawnPoint(area.PreviewWorldXZ, area.PreviewEulerDeg);
                    EditorUtility.SetDirty(area);
                    SceneView.RepaintAll();
                }
                using (new EditorGUI.DisabledScope(area.FixedSpawnPointCount == 0))
                {
                    if (GUILayout.Button($"Clear all ({area.FixedSpawnPointCount})"))
                    {
                        Undo.RecordObject(area, "Clear fixed spawn points");
                        area.ClearFixedSpawnPoints();
                        EditorUtility.SetDirty(area);
                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Scene view:\n" +
                "• Blue dots = polygon vertices. Drag to reshape.\n" +
                "• Yellow dots between vertices = click to insert a new vertex.\n" +
                "• Orange ghost = main preview; sets the random-fallback rotation.\n" +
                "• Cyan ghosts = fixed spawn points (claim pool).\n" +
                "• Each ghost: drag the position arrows at its feet; rotate via the rings.\n\n" +
                "Runtime:\n" +
                "• First N players get random un-claimed fixed slots (each with its own pos + rot).\n" +
                "• Once the pool is empty, fallback is random inside the polygon, baked rotation.\n" +
                "• 0 fixed slots = every player rolls random.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            var area = (PlayerSpawnArea)target;
            if (area == null) return;

            DrawPolygonHandles(area);
            DrawMidpointInsertButtons(area);
            DrawPreviewHandles(area);
            DrawFixedSpawnHandles(area);
        }

        private void DrawPolygonHandles(PlayerSpawnArea area)
        {
            float planeY = area.transform.position.y;
            for (int i = 0; i < area.VertexCount; i++)
            {
                Vector3 world = area.LocalToWorld(area.GetVertex(i));
                world.y = planeY;
                float size = HandleUtility.GetHandleSize(world) * 0.08f;

                EditorGUI.BeginChangeCheck();
                Handles.color = new Color(0.5f, 0.85f, 1f, 1f);
                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    moved.y = planeY;
                    Undo.RecordObject(area, "Move spawn-area vertex");
                    area.SetVertex(i, area.WorldToLocal(moved));
                    EditorUtility.SetDirty(area);
                }
            }
        }

        private void DrawMidpointInsertButtons(PlayerSpawnArea area)
        {
            float planeY = area.transform.position.y;
            int n = area.VertexCount;
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vector2 mid = (area.GetVertex(i) + area.GetVertex(next)) * 0.5f;
                Vector3 midWorld = area.LocalToWorld(mid);
                midWorld.y = planeY + 0.05f;
                float size = HandleUtility.GetHandleSize(midWorld) * 0.045f;
                Handles.color = new Color(1f, 1f, 0.3f, 0.95f);
                if (Handles.Button(midWorld, Quaternion.identity, size, size, Handles.SphereHandleCap))
                {
                    Undo.RecordObject(area, "Insert spawn-area vertex");
                    area.InsertVertex(next, mid);
                    EditorUtility.SetDirty(area);
                }
            }
        }

        private void DrawPreviewHandles(PlayerSpawnArea area)
        {
            Vector3 foot = ResolveFootWorldPos(area, area.PreviewWorldXZ);

            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(1f, 0.7f, 0.3f, 1f);
            Vector3 moved = Handles.PositionHandle(foot, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(area, "Move preview spawn point");
                area.PreviewWorldXZ = new Vector2(moved.x, moved.z);
                EditorUtility.SetDirty(area);
            }

            EditorGUI.BeginChangeCheck();
            Quaternion newRot = Handles.RotationHandle(area.PreviewRotation, foot);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(area, "Rotate preview spawn");
                area.PreviewRotation = newRot;
                EditorUtility.SetDirty(area);
            }
        }

        private void DrawFixedSpawnHandles(PlayerSpawnArea area)
        {
            int n = area.FixedSpawnPointCount;
            for (int i = 0; i < n; i++)
            {
                var fp = area.GetFixedSpawnPoint(i);
                Vector3 foot = ResolveFootWorldPos(area, fp.WorldXZ);

                Handles.color = new Color(0.4f, 0.9f, 1f, 1f);
                Handles.Label(foot + Vector3.up * 2.3f, $"Fixed #{i}");

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(foot, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(area, "Move fixed spawn point");
                    area.SetFixedSpawnPointPos(i, new Vector2(moved.x, moved.z));
                    EditorUtility.SetDirty(area);
                }

                EditorGUI.BeginChangeCheck();
                Quaternion newRot = Handles.RotationHandle(Quaternion.Euler(fp.EulerDeg), foot);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(area, "Rotate fixed spawn point");
                    area.SetFixedSpawnPointEuler(i, newRot.eulerAngles);
                    EditorUtility.SetDirty(area);
                }
            }
        }

        private static Vector3 ResolveFootWorldPos(PlayerSpawnArea area, Vector2 worldXZ)
        {
            return area.TryResolveGround(worldXZ, out var ground)
                ? ground
                : new Vector3(worldXZ.x, area.transform.position.y, worldXZ.y);
        }
    }
}
#endif
