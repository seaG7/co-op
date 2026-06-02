#if UNITY_EDITOR
using FishNet.Object;
using Gameplay.World.Items;
using UnityEditor;
using UnityEngine;
using Zenject;

namespace EditorTools
{
    public static class CarryTestMenu
    {
        [MenuItem("Tools/CoOp/Carry/Spawn Test Items", false, 100)]
        private static void SpawnTestItems()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[CarryTestMenu] Enter Play Mode first."); return; }
            var ctx = ProjectContext.Instance;
            if (ctx == null) { Debug.LogError("[CarryTestMenu] ProjectContext not initialised."); return; }
            var spawner = ctx.Container.TryResolve<Infrastructure.Services.Spawn.INetworkSpawnService>();
            var network  = ctx.Container.TryResolve<Infrastructure.Services.Network.INetworkService>();
            if (spawner == null || network == null || !network.IsServer)
            { Debug.LogError("[CarryTestMenu] Run on the host (server) editor."); return; }

            var configs = Resources.LoadAll<Data.Configs.InteractableItemConfig>("Configs/Interactables");
            if (configs == null || configs.Length == 0)
            {
                Debug.LogWarning("[CarryTestMenu] No InteractableItemConfig assets found under Resources/Configs/Interactables/. Create at least one to use this menu.");
                return;
            }

            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[CarryTestMenu] No main camera."); return; }
            var basePos = cam.transform.position + cam.transform.forward * 1.5f;

            int spawned = 0;
            for (int i = 0; i < configs.Length; i++)
            {
                var config = configs[i];
                if (config.Prefab == null) continue;
                spawner.SpawnNetworked(config.Prefab, basePos + Vector3.right * (i * 0.7f), Quaternion.identity, owner: null);
                spawned++;
            }
            Debug.Log($"[CarryTestMenu] Spawned {spawned} test items.");
        }

        [MenuItem("Tools/CoOp/Carry/Despawn All Items", false, 101)]
        private static void DespawnAllItems()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[CarryTestMenu] Enter Play Mode first."); return; }
            var network = ProjectContext.Instance?.Container.TryResolve<Infrastructure.Services.Network.INetworkService>();
            if (network == null || !network.IsServer) { Debug.LogError("[CarryTestMenu] Run on the host."); return; }
            int n = 0;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Carryable>(FindObjectsSortMode.None))
            {
                var nob = c.GetComponent<NetworkObject>();
                if (nob == null) continue;
                network.NetworkManager.ServerManager.Despawn(nob.gameObject);
                n++;
            }
            Debug.Log($"[CarryTestMenu] Despawned {n} items.");
        }
    }
}
#endif
