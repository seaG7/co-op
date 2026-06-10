#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

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
                var sb = new StringBuilder();
                sb.AppendLine("[PlayerPrefabDump] structure + colliders of " + PrefabPath);
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var col = t.GetComponent<Collider>();
                    bool isBone = t.name.StartsWith("CC_Base_");
                    if (isBone && col == null) continue;
                    var comps = t.GetComponents<Component>();
                    var names = new StringBuilder();
                    foreach (var c in comps)
                    {
                        if (c == null) { names.Append("<null> "); continue; }
                        names.Append(c.GetType().Name);
                        if (c is Collider cc) names.Append(cc.enabled ? "(on)" : "(off)");
                        names.Append(' ');
                    }
                    sb.AppendLine($"  {GetPath(t)} | [{names.ToString().TrimEnd()}] | pos={t.localPosition} eul={t.localEulerAngles}");
                }
                Debug.Log(sb.ToString());
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
