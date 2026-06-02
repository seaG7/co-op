#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MicroWorldNS
{
    public static class PrefsEditor
    {
        private static Vector2 scrollPosition;
        private static bool forceSave;
        public const string Path = "Project/MicroWorld";

        [SettingsProvider]
        public static SettingsProvider GetSettingsProvider()
        {
            SettingsProvider provider = new SettingsProvider(Path, SettingsScope.Project)
            {
                label = "MicroWorld",
                guiHandler = DrawGeneralManagers,
                keywords = new string[] { "MicroWorld", "Micro World" }
            };
            return provider;
        }

        public static void DrawGeneralManagers(string searchContext)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            try
            {
                EditorGUI.BeginChangeCheck();
                Draw();
                EditorGUILayout.Space();
                if (EditorGUI.EndChangeCheck() || forceSave)
                {
                    EditorUtility.SetDirty(Preferences.Instance);
                    forceSave = false;
                }
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch
            {
            }

            EditorGUILayout.EndScrollView();
        }

        private static void Draw()
        {
            var prefs = Preferences.Instance;

            prefs.MaxPassageHeight = EditorGUILayout.FloatField("Max Passage Height", prefs.MaxPassageHeight);
            prefs.InclineWalkableAngle = EditorGUILayout.FloatField("Incline Walkable Angle", prefs.InclineWalkableAngle);
            prefs.DeclineWalkableAngle = EditorGUILayout.FloatField("Decline Walkable Angle", prefs.DeclineWalkableAngle);
            
            prefs.StepHeight = EditorGUILayout.FloatField("Step Height", prefs.StepHeight);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scale Density and Noise Proportionally To Cell Size");
            prefs.ScaleLandscapeNoiseAmplProportionallyToCellSize = EditorGUILayout.Slider("Landscape Noise Ampl", prefs.ScaleLandscapeNoiseAmplProportionallyToCellSize, 0, 1);
            prefs.ScaleSpawnCountProportionallyToCellSize = EditorGUILayout.Slider("Spawn Density", prefs.ScaleSpawnCountProportionallyToCellSize, 0, 1);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Background build");
            prefs.MaxBuildDutyPerFrameInMs = EditorGUILayout.FloatField("Max Build Duty (ms per frame)", prefs.MaxBuildDutyPerFrameInMs);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug");
            prefs.LogFeatures = (LogFeatures)EditorGUILayout.EnumFlagsField("Log Options", prefs.LogFeatures);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scale prefabs");
            prefs.ScaleGrassWidth = EditorGUILayout.Slider("Grass Width", prefs.ScaleGrassWidth, 0, 2);
            prefs.ScaleGrassHeight = EditorGUILayout.Slider("Grass Height", prefs.ScaleGrassHeight, 0, 2);
            prefs.ScalePlants = EditorGUILayout.Slider("Plants Scale", prefs.ScalePlants, 0, 2);
            prefs.ScaleBushes = EditorGUILayout.Slider("Bushes Scale", prefs.ScaleBushes, 0, 2);
            prefs.ScaleTrees = EditorGUILayout.Slider("Trees Scale", prefs.ScaleTrees, 0, 2);
            prefs.ScaleSticks = EditorGUILayout.Slider("Sticks Scale", prefs.ScaleSticks, 0, 2);
            prefs.ScaleRocks = EditorGUILayout.Slider("Rocks Scale", prefs.ScaleRocks, 0, 2);

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset to Default"))
            {
                Preferences.Reset();
                forceSave = true;
            }
        }
    }
}
#endif