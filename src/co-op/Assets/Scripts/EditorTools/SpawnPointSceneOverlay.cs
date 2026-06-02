#if UNITY_EDITOR
using Gameplay.Spawn;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    [InitializeOnLoad]
    public static class SpawnPointSceneOverlay
    {
        static SpawnPointSceneOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView view)
        {
            var points = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            if (points == null || points.Length < 2) return;

            Handles.color = new Color(1, 1, 1, 0.18f);
            for (int i = 0; i < points.Length; i++)
                Handles.DrawLine(points[i].Position, points[(i + 1) % points.Length].Position);
        }
    }
}
#endif
