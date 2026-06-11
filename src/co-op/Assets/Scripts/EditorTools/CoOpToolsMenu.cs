#if UNITY_EDITOR
using System.Collections.Generic;
using Data.Configs;
using Data.Paths;
using Data.UI;
using FishNet.Object;
using Gameplay.Player;
using Gameplay.Spawn;
using Infrastructure.Installers;
using Infrastructure.Services.UI;
using UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zenject;

namespace EditorTools
{
    public static class CoOpToolsMenu
    {

        [MenuItem("Tools/CoOp/Scenes/Open Bootstrap")]
        private static void OpenBootstrap() => OpenScene(ScenePaths.BOOTSTRAP_SCENE);

        [MenuItem("Tools/CoOp/Scenes/Open MainMenu")]
        private static void OpenMainMenu() => OpenScene(ScenePaths.MAIN_MENU_SCENE);

        [MenuItem("Tools/CoOp/Scenes/Open Game")]
        private static void OpenGame() => OpenScene(ScenePaths.GAME_SCENE);

        [MenuItem("Tools/CoOp/Validate/All")]
        private static void ValidateAll()
        {
            ValidateWindowsConfig();
            ValidatePlayerPrefab();
            ValidateSpawnPoints();
            Debug.Log("[CoOp] Validation finished.");
        }

        [MenuItem("Tools/CoOp/Validate/Windows Config")]
        private static void ValidateWindowsConfig()
        {
            var cfg = Resources.Load<WindowsConfig>(ConfigPaths.WINDOWS_CONFIG_PATH);
            if (cfg == null)
            {
                Debug.LogError($"[CoOp] WindowsConfig not found at Resources/{ConfigPaths.WINDOWS_CONFIG_PATH}.");
                return;
            }

            var seen = new HashSet<WindowID>();
            var missing = new HashSet<WindowID>();
            foreach (WindowID id in System.Enum.GetValues(typeof(WindowID)))
                if (id != WindowID.None && id != WindowID.Unknown) missing.Add(id);

            if (cfg.windows != null)
            {
                foreach (var rec in cfg.windows)
                {
                    if (rec == null) continue;
                    if (!seen.Add(rec.windowID))
                        Debug.LogError($"[CoOp] WindowsConfig: duplicate WindowID {rec.windowID}.", cfg);
                    if (rec.prefab == null)
                        Debug.LogError($"[CoOp] WindowsConfig: prefab for {rec.windowID} is null.", cfg);
                    else if (rec.prefab.GetComponent<WindowView>() == null)
                        Debug.LogError($"[CoOp] {rec.prefab.name} has no WindowView component (id {rec.windowID}).", cfg);

                    missing.Remove(rec.windowID);
                }
            }

            if (missing.Count > 0)
                Debug.LogWarning($"[CoOp] WindowsConfig missing entries: {string.Join(", ", missing)}", cfg);
            else
                Debug.Log("[CoOp] WindowsConfig OK.", cfg);
        }

        [MenuItem("Tools/CoOp/Validate/Player Prefab")]
        private static void ValidatePlayerPrefab()
        {
            var guids = AssetDatabase.FindAssets("Player t:Prefab");
            GameObject prefab = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/Player.prefab")) continue;
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                break;
            }

            if (prefab == null)
            {
                Debug.LogError("[CoOp] Player.prefab not found in project.");
                return;
            }

            if (prefab.GetComponent<NetworkObject>() == null)
                Debug.LogError("[CoOp] Player.prefab is missing NetworkObject.", prefab);
            if (prefab.GetComponent<PlayerNetwork>() == null)
                Debug.LogError("[CoOp] Player.prefab is missing PlayerNetwork.", prefab);
            if (prefab.GetComponent<GameObjectContext>() != null)
                Debug.LogWarning("[CoOp] Player.prefab still has a GameObjectContext — it is no longer used " +
                                 "(players inject via the scene-container registry like other networked objects). Remove it.", prefab);

            Debug.Log("[CoOp] Player.prefab validation complete.", prefab);
        }

        [MenuItem("Tools/CoOp/Validate/SpawnPoints in Active Scene")]
        private static void ValidateSpawnPoints()
        {
            var points = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            if (points.Length == 0)
                Debug.LogError("[CoOp] Active scene has no SpawnPoint markers.");
            else
                Debug.Log($"[CoOp] Active scene has {points.Length} SpawnPoint(s).");
        }

        [MenuItem("Tools/CoOp/Playmode/Start Host (localhost)")]
        private static void PlayHost()
        {
            EditorPrefs.SetString("CoOp.LaunchMode", "Host");
            EnsureBootstrapAndPlay();
        }

        [MenuItem("Tools/CoOp/Playmode/Start Client (localhost)")]
        private static void PlayClient()
        {
            EditorPrefs.SetString("CoOp.LaunchMode", "Client");
            EnsureBootstrapAndPlay();
        }

        [MenuItem("Tools/CoOp/Playmode/Start Dedicated Server")]
        private static void PlayServer()
        {
            EditorPrefs.SetString("CoOp.LaunchMode", "Server");
            EnsureBootstrapAndPlay();
        }

        private static void EnsureBootstrapAndPlay()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                Debug.LogWarning("[CoOp] Was playing — stopped. Press the menu item again to start fresh.");
                return;
            }

            var activeName = EditorSceneManager.GetActiveScene().name;
            if (activeName != ScenePaths.BOOTSTRAP_SCENE)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    var path = FindScenePath(ScenePaths.BOOTSTRAP_SCENE);
                    if (string.IsNullOrEmpty(path))
                    {
                        Debug.LogError("[CoOp] Bootstrap scene not found in project.");
                        return;
                    }
                    EditorSceneManager.OpenScene(path);
                }
                else return;
            }
            EditorApplication.isPlaying = true;
        }

        private static void OpenScene(string sceneName)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var path = FindScenePath(sceneName);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[CoOp] Scene '{sceneName}' not found.");
                return;
            }
            EditorSceneManager.OpenScene(path);
        }

        private static string FindScenePath(string sceneName)
        {
            var guids = AssetDatabase.FindAssets($"{sceneName} t:Scene");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith($"/{sceneName}.unity")) return path;
            }
            return null;
        }
    }
}
#endif
