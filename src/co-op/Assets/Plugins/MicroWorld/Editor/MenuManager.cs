#if UNITY_EDITOR
using MicroWorldNS.Spawners;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroWorldNS
{
    static class MenuManager
    {
        const string GameObject = "GameObject/Micro World/";
        const string ComponentMicroWorld = "Component/Micro World/";
        public const string MainMenu = "Tools/Micro World/";

        [MenuItem(ComponentMicroWorld + "Micro World", priority = 20)]
        static void MicroWorldComponent() { var mw = Selection.activeTransform.gameObject.AddComponent<MicroWorld>(); mw.Seed = Random.Range(0, 100000); }
        [MenuItem(ComponentMicroWorld + "Micro World", true)]
        static bool ValidateMicroWorldComponent() => Selection.activeTransform != null;

        [MenuItem(ComponentMicroWorld + "Terrain Spawner", priority = 20)]
        static void TerrainSpawner() => Selection.activeTransform.gameObject.AddComponent<TerrainSpawner>();
        [MenuItem(ComponentMicroWorld + "Terrain Spawner", true)]
        static bool ValidateTerrainSpawner() => Selection.activeTransform != null;

        [MenuItem(ComponentMicroWorld + "Cell Spawner", priority = 20)]
        static void CellSpawner() => Selection.activeTransform.gameObject.AddComponent<CellSpawner>();
        [MenuItem(ComponentMicroWorld + "Cell Spawner", true)]
        static bool ValidateCellSpawner() => Selection.activeTransform != null;

        [MenuItem(ComponentMicroWorld + "Buildings Spawner", priority = 20)]
        static void BuildingsSpawner() => Selection.activeTransform.gameObject.AddComponent<BuildingsSpawner>();
        [MenuItem(ComponentMicroWorld + "Buildings Spawner", true)]
        static bool ValidateBuildingsSpawner() => Selection.activeTransform != null;

        [MenuItem(ComponentMicroWorld + "Room Spawner", priority = 20)]
        static void RoomSpawner() => Selection.activeTransform.gameObject.AddComponent<RoomSpawner>();
        [MenuItem(ComponentMicroWorld + "Room Spawner", true)]
        static bool ValidateRoomSpawner() => Selection.activeTransform != null;

        [MenuItem(ComponentMicroWorld + "Border Collider Spawner", priority = 20)]
        static void BorderColliderSpawner() => Selection.activeTransform.gameObject.AddComponent<BorderColliderSpawner>();
        [MenuItem(ComponentMicroWorld + "Border Collider Spawner", true)]
        static bool ValidateBorderColliderSpawner() => Selection.activeTransform != null;

        [MenuItem(ComponentMicroWorld + "Gate Spawner", priority = 20)]
        static void GateSpawner() => Selection.activeTransform.gameObject.AddComponent<GateSpawner>();
        [MenuItem(ComponentMicroWorld + "Gate Spawner", true)]
        static bool ValidateGateSpawner() => Selection.activeTransform != null;

        [MenuItem(ComponentMicroWorld + "Grass Spawner", priority = 20)]
        static void GrassSpawner() => Selection.activeTransform.gameObject.AddComponent<GrassSpawner>();
        [MenuItem(ComponentMicroWorld + "Grass Spawner", true)]
        static bool ValidateGrassSpawner() => Selection.activeTransform != null;

        [MenuItem(ComponentMicroWorld + "Map Spawner", priority = 20)]
        static void MapSpawner() => Selection.activeTransform.gameObject.AddComponent<MapSpawner>();
        [MenuItem(ComponentMicroWorld + "Map Spawner", true)]
        static bool ValidateMapSpawner() => Selection.activeTransform != null;

        [MenuItem(ComponentMicroWorld + "Surface Spawner", priority = 20)]
        static void SurfaceSpawner() => Selection.activeTransform.gameObject.AddComponent<SurfaceSpawner>();
        [MenuItem(ComponentMicroWorld + "Surface Spawner", true)]
        static bool ValidateSurfaceSpawner() => Selection.activeTransform != null;

        [MenuItem(MainMenu + "Build Terrain _F5", priority = 5)]
        static void BuildFromMenu()
        {
            if (Selection.activeObject is GameObject go)
            {
                var world = go.GetComponentInParent<MicroWorld>(true);
                if (world)
                {
                    world.BuildInEditor();
                    UnityEditor.EditorUtility.SetDirty(world.gameObject);
                    return;
                }
            }
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                var mw = scene.GetRootGameObjects().Where(go => go.activeInHierarchy).Select(go => go.GetComponent<MicroWorld>()).FirstOrDefault(m => m != null && m.enabled);
                if (mw)
                {
                    mw.BuildInEditor();
                    UnityEditor.EditorUtility.SetDirty(mw.gameObject);
                }
            }

            //var pos = UnityEditor.SceneView.lastActiveSceneView.pivot;
            //if (pos.x < 0 || pos.z < 0)
            //{
            //    UnityEditor.SceneView.lastActiveSceneView.pivot = new Vector3(100, 30, 100);
            //}
        }

        [MenuItem(GameObject + "Micro World", priority = 20)]
        [MenuItem(MainMenu + "Create/Micro World", priority = 7)]
        static void MicroWorld()
        {
            var go = new GameObject("MicroWorld", typeof(MicroWorld));
            var mw = go.GetComponent<MicroWorld>();
            mw.Seed = Random.Range(0, 100000);
            AddDefaultSpawners(mw);
            Selection.activeObject = mw;
        }

        [MenuItem(GameObject + "Micro World On Separate Scene", priority = 21)]
        [MenuItem(MainMenu + "Create/Micro World On Separate Scene", priority = 8)]
        static void MicroWorldOnScene()
        {
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var go = new GameObject("MicroWorld", typeof(MicroWorld));
            go.isStatic = true;
            SceneManager.MoveGameObjectToScene(go, newScene);

            var mw = go.GetComponent<MicroWorld>();
            mw.Seed = Random.Range(0, 100000);
            AddDefaultSpawners(mw);
            Selection.activeObject = mw;

            newScene.name = "NewLevel";
            EditorSceneManager.MarkSceneDirty(newScene);
        }

        [MenuItem(GameObject + "Gate Manager", priority = 22)]
        [MenuItem(MainMenu + "Create/Gate Manager", priority = 9)]
        static void GateManager()
        {
            var go = new GameObject("GateManager", typeof(BaseGateManager));
            var gm = go.GetComponent<BaseGateManager>();
            Selection.activeObject = gm;
        }

        [MenuItem(GameObject + "Cell Spawner", priority = 22)]
        [MenuItem(MainMenu + "Create/Cell Spawner", priority = 10)]
        static void CellSpawnerObj()
        {
            var parent = (Selection.activeObject as GameObject)?.transform;
            if (parent != null && parent.parent != null)
                parent = parent.parent;

            var go = new GameObject("CellSpawner", typeof(CellSpawner));
            var gm = go.GetComponent<CellSpawner>();
            gm.transform.SetParent(parent, false);
            Selection.activeObject = gm;
        }

        [MenuItem(GameObject + "Buildings Spawner", priority = 22)]
        [MenuItem(MainMenu + "Create/Buildings Spawner", priority = 11)]
        static void BuildingsSpawnerObj()
        {
            var parent = (Selection.activeObject as GameObject)?.transform;
            if (parent != null && parent.parent != null)
                parent = parent.parent;

            var go = new GameObject("BuildingsSpawner", typeof(BuildingsSpawner), typeof(RoomSpawner));
            var gm = go.GetComponent<BuildingsSpawner>();
            go.transform.SetParent(parent, false);
            Selection.activeObject = gm;
        }

        [MenuItem(GameObject + "Terrain Spawner", priority = 22)]
        [MenuItem(MainMenu + "Create/Terrain Spawner", priority = 11)]
        static void TerrainSpawnerObj()
        {
            var parent = (Selection.activeObject as GameObject)?.transform;
            if (parent != null && parent.parent != null)
                parent = parent.parent;

            var go = new GameObject("TerrainSpawner", typeof(TerrainSpawner));
            var gm = go.GetComponent<TerrainSpawner>();
            go.transform.SetParent(parent, false);
            Selection.activeObject = gm;
        }

        [MenuItem(GameObject + "Cell Modifier", priority = 23)]
        [MenuItem(MainMenu + "Create/Cell Modifier", priority = 12)]
        static void CellModifierObj()
        {
            var parent = (Selection.activeObject as GameObject)?.transform;
            if (parent != null && parent.parent != null)
                parent = parent.parent;

            var go = new GameObject("CellModifier", typeof(CellModifier));
            var gm = go.GetComponent<CellModifier>();
            go.transform.SetParent(parent, false);
            Selection.activeObject = gm;
        }

        static void AddDefaultSpawners(MicroWorld world)
        {
            var types = MicroWorldHelper.GetDefaultSpawners();
            var go = new GameObject("Default Spawners", types);
            go.transform.SetParent(world.transform, false);
            go.isStatic = world.gameObject.isStatic;
        }


        [MenuItem(MainMenu + "Preferences", false, 80)]
        public static void Prefs()
        {
            SettingsService.OpenProjectSettings(PrefsEditor.Path);
        }
    }
}
#endif