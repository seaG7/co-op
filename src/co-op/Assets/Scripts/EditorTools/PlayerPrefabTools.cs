#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CoOp.EditorTools
{
    public static class PlayerPrefabTools
    {
        private const string PrefabPath = "Assets/Prefabs/Player.prefab";

        [MenuItem("Tools/CoOp/Dump Player Prefab")]
        public static void Dump()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var cols = root.GetComponentsInChildren<Collider>(true);
                var cs = new StringBuilder("[PlayerDumpColliders] count=" + cols.Length + ": ");
                foreach (var c in cols)
                    cs.Append(c.transform.name + "<" + c.GetType().Name + (c.enabled ? ",on" : ",off") + (c.isTrigger ? ",trig" : "") + "> ");
                Debug.Log(cs.ToString());

                var anchor = FindChild(root.transform, "CarryAnchor");
                Debug.Log("[PlayerDumpAnchor] " + (anchor != null
                    ? "path=" + GetPath(anchor) + " localPos=" + anchor.localPosition + " parent=" + (anchor.parent != null ? anchor.parent.name : "-")
                    : "CarryAnchor MISSING"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private const string FbxPath = "Assets/Prefabs/Samurai/SamuraiBoyRed.fbx";
        private const string ControllerPath = "Assets/Prefabs/PlayerAnimator.controller";

        [MenuItem("Tools/CoOp/Wire Player Prefab")]
        public static void Wire()
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Avatar>().FirstOrDefault();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[PlayerPrefabWire] controller not found at " + ControllerPath); return; }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var anim = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                if (avatar != null) anim.avatar = avatar;
                anim.applyRootMotion = false;

                var movement = root.GetComponent("PlayerMovement");
                var carry = root.GetComponent("PlayerCarry");
                var vitals = root.GetComponent("PlayerVitals");
                var camRig = root.GetComponent("PlayerCameraRig");

                var pAnim = EnsureComponent(root, "Gameplay.Player.Animation.PlayerAnimator");
                var pHandIk = EnsureComponent(root, "Gameplay.Player.Animation.PlayerHandIK");
                EnsureComponent(root, "Gameplay.Player.View.PlayerModelVisibility");

                SetRef(pAnim, "_animator", anim);
                SetRef(pAnim, "_movement", movement);
                SetRef(pAnim, "_carry", carry);
                SetRef(pAnim, "_vitals", vitals);

                SetRef(pHandIk, "_animator", anim);
                SetRef(pHandIk, "_carry", carry);

                var headBone = FindChild(root.transform, "CC_Base_Head");

                float headY = headBone != null ? root.transform.InverseTransformPoint(headBone.position).y : 1.6f;

                if (camRig != null)
                    SetVec3(camRig, "_localCameraOffset", new Vector3(0f, headY - 0.05f, 0.18f));

                var anchor = FindChild(root.transform, "CarryAnchor");
                if (anchor == null)
                {
                    var ago = new GameObject("CarryAnchor");
                    ago.transform.SetParent(root.transform, false);
                    anchor = ago.transform;
                }
                anchor.localPosition = new Vector3(0f, headY * 0.72f, 0.45f);
                anchor.localRotation = Quaternion.identity;
                SetRef(root.GetComponent("PlayerCarry"), "_carryAnchor", anchor);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[PlayerPrefabWire] done. avatar={(avatar != null)}, headBone={(headBone != null)}, " +
                          $"carryAnchor set, camera eye-forward; movement={(movement != null)}, carry={(carry != null)}, " +
                          $"vitals={(vitals != null)}, camRig={(camRig != null)}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/CoOp/Add Carry Grips")]
        public static void AddCarryGrips()
        {
            var carryType = FindType("Gameplay.World.Items.Carryable");
            if (carryType == null) { Debug.LogError("[CarryGrips] Carryable type not found."); return; }

            int done = 0, skipped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || asset.GetComponent(carryType) == null) continue;

                var probe = new SerializedObject(asset.GetComponent(carryType));
                var lp = probe.FindProperty("_leftHandGrip");
                var rp = probe.FindProperty("_rightHandGrip");
                if (lp == null || rp == null) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var carry = root.GetComponent(carryType);
                    var b = LocalBounds(root);
                    float ex = Mathf.Max(0.05f, b.extents.x) + 0.04f;

                    var left = GetOrCreateChild(root.transform, "LeftHandGrip");
                    var right = GetOrCreateChild(root.transform, "RightHandGrip");
                    left.localPosition = new Vector3(b.center.x - ex, b.center.y, b.center.z);
                    right.localPosition = new Vector3(b.center.x + ex, b.center.y, b.center.z);
                    left.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.right);
                    right.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.left);

                    SetRef(carry, "_leftHandGrip", left);
                    SetRef(carry, "_rightHandGrip", right);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                    Debug.Log($"[CarryGrips] {path}: grips at +-{ex:F2} x (DEFAULT — tune by hand).");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log($"[CarryGrips] done={done}, skipped(existing)={skipped}.");
        }

        private static Bounds LocalBounds(GameObject root)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.3f);
            var acc = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) acc.Encapsulate(rends[i].bounds);
            var centerLocal = root.transform.InverseTransformPoint(acc.center);
            return new Bounds(centerLocal, acc.size);
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        [MenuItem("Tools/CoOp/Build Drunk Volume")]
        public static void BuildDrunkVolume()
        {
            const string profilePath = "Assets/Prefabs/DrunkVolumeProfile.asset";
            const string prefabPath = "Assets/Prefabs/DrunkVolume.prefab";

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(profilePath))
                if (sub is VolumeComponent vc) UnityEngine.Object.DestroyImmediate(vc, true);
            profile.components.Clear();

            var ca = AddOverride<ChromaticAberration>(profile);
            ca.intensity.overrideState = true; ca.intensity.value = 1f;

            var ld = AddOverride<LensDistortion>(profile);
            ld.intensity.overrideState = true; ld.intensity.value = -0.35f;
            ld.scale.overrideState = true; ld.scale.value = 1f;

            var vg = AddOverride<Vignette>(profile);
            vg.intensity.overrideState = true; vg.intensity.value = 0.45f;
            vg.smoothness.overrideState = true; vg.smoothness.value = 0.55f;

            var dof = AddOverride<DepthOfField>(profile);
            dof.mode.overrideState = true; dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.overrideState = true; dof.gaussianStart.value = 8f;
            dof.gaussianEnd.overrideState = true; dof.gaussianEnd.value = 32f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(profilePath);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var go = new GameObject("DrunkVolume");
                try
                {
                    var vol = go.AddComponent<Volume>();
                    vol.isGlobal = true;
                    vol.priority = 100f;
                    vol.weight = 0f;
                    vol.sharedProfile = profile;

                    var fxType = FindType("Gameplay.Player.View.DrunkPostFx");
                    var fx = fxType != null ? go.AddComponent(fxType) : null;
                    SetRef(fx, "_volume", vol);

                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
                Debug.Log($"[DrunkVolume] profile + prefab created at {prefabPath}. Drop it into the scene.");
            }
            else
            {
                Debug.Log($"[DrunkVolume] profile rebuilt with {profile.components.Count} overrides " +
                          "(prefab left intact — existing scene instance keeps working).");
            }
        }

        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var comp = ScriptableObject.CreateInstance<T>();
            comp.name = typeof(T).Name;
            comp.hideFlags = HideFlags.HideInHierarchy;
            profile.components.Add(comp);
            AssetDatabase.AddObjectToAsset(comp, profile);
            return comp;
        }

        [MenuItem("Tools/CoOp/Wire Drink Mechanic")]
        public static void WireDrink()
        {
            int interactLayer = 0;
            var ci = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/Items/ComponentItem.prefab");
            if (ci != null) interactLayer = ci.layer;

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var pDrink = EnsureComponent(root, "Gameplay.Player.Combat.PlayerDrink");
                EnsureComponent(root, "Gameplay.Player.Vitals.PlayerDrunk");

                var hand = FindChild(root.transform, "CC_Base_R_Hand");
                Transform anchor = FindChild(root.transform, "DrinkAnchor");
                if (hand != null)
                {
                    if (anchor == null) { var go = new GameObject("DrinkAnchor"); go.transform.SetParent(hand, false); anchor = go.transform; }
                    else if (anchor.parent != hand) anchor.SetParent(hand, false);
                    anchor.localPosition = new Vector3(0.04f, 0.03f, 0.03f);
                    anchor.localRotation = Quaternion.identity;
                }
                else Debug.LogWarning("[WireDrink] CC_Base_R_Hand not found on Player.");

                SetRef(pDrink, "_drinkAnchor", anchor);
                SetLayerMask(pDrink, "_drinkableMask", 1 << interactLayer);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            BuildBottlePrefab(interactLayer);
            Debug.Log($"[WireDrink] done. interactLayer={interactLayer} ({UnityEngine.LayerMask.LayerToName(interactLayer)}).");
        }

        private static void BuildBottlePrefab(int layer)
        {
            const string outPath = "Assets/Prefabs/World/Items/BottleDrinkable.prefab";
            const string visSrc = "Assets/DownloadedAssets/ToonScapes/Spring Isles/Prefabs/Props/Tea_Set/TSI_Bottle_01A.prefab";

            var rootGo = new GameObject("BottleDrinkable");
            try
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(visSrc);
                if (src != null)
                {
                    var vis = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    vis.transform.SetParent(rootGo.transform, false);
                }
                else Debug.LogWarning($"[WireDrink] bottle visual not found at {visSrc}.");

                var col = rootGo.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.12f, 0f);
                col.size = new Vector3(0.1f, 0.24f, 0.1f);
                var rb = rootGo.AddComponent<Rigidbody>();
                rb.mass = 0.5f;

                var noType = FindType("FishNet.Object.NetworkObject");
                if (noType != null) rootGo.AddComponent(noType);
                var ntType = FindType("FishNet.Component.Transforming.NetworkTransform");
                if (ntType != null) rootGo.AddComponent(ntType);
                var drinkType = FindType("Gameplay.World.Items.Drinkable");
                var drink = drinkType != null ? rootGo.AddComponent(drinkType) : null;

                var grip = new GameObject("Grip");
                grip.transform.SetParent(rootGo.transform, false);
                grip.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                SetRef(drink, "_grip", grip.transform);

                SetLayerRecursive(rootGo, layer);

                PrefabUtility.SaveAsPrefabAsset(rootGo, outPath);
                Debug.Log($"[WireDrink] bottle prefab saved: {outPath} (visual={(src != null)}).");
            }
            finally { UnityEngine.Object.DestroyImmediate(rootGo); }
        }

        private static void SetLayerMask(Component c, string prop, int mask)
        {
            if (c == null) return;
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[PlayerPrefabWire] no property {prop} on {c.GetType().Name}"); return; }
            p.intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        private static Component EnsureComponent(GameObject go, string fullTypeName)
        {
            var t = FindType(fullTypeName);
            if (t == null) { Debug.LogError("[PlayerPrefabWire] type not found: " + fullTypeName); return null; }
            var c = go.GetComponent(t);
            return c != null ? c : go.AddComponent(t);
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(fullName)).FirstOrDefault(x => x != null);
        }

        private static void SetRef(Component c, string prop, UnityEngine.Object value)
        {
            if (c == null) return;
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[PlayerPrefabWire] no property {prop} on {c.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVec3(Component c, string prop, Vector3 v)
        {
            if (c == null) return;
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[PlayerPrefabWire] no property {prop} on {c.GetType().Name}"); return; }
            p.vector3Value = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static string GetPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
            return sb.ToString();
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            string indent = new string(' ', depth * 2);
            var comps = t.GetComponents<Component>();
            var names = new StringBuilder();
            foreach (var c in comps)
            {
                if (c == null) { names.Append("<MISSING> "); continue; }
                names.Append(c.GetType().Name);
                if (c is SkinnedMeshRenderer smr)
                {
                    names.Append("{");
                    var mats = smr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        names.Append((mats[i] != null ? mats[i].name : "null") + (i < mats.Length - 1 ? "," : ""));
                    names.Append("}");
                }
                names.Append(' ');
            }
            sb.AppendLine($"{indent}{t.name}  [{names.ToString().TrimEnd()}]  localPos={t.localPosition}");
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1, sb);
        }
    }
}
#endif
