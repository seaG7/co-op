#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CoOp.EditorTools
{
    public static class UiRaycastTools
    {
        private static readonly string[] Prefabs =
        {
            "Assets/Prefabs/UI/MainMenuWindow.prefab",
            "Assets/Prefabs/UI/LobbyWindow.prefab",
            "Assets/Prefabs/UI/GameOverScreen.prefab",
        };

        [MenuItem("Tools/CoOp/Dump UI Raycast")]
        public static void DumpUiRaycast()
        {
            var sb = new StringBuilder();
            foreach (var path in Prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    sb.AppendLine("=== " + path + " ===");
                    Walk(root.transform, 0, sb);
                    sb.AppendLine();
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var outPath = Path.Combine(projectRoot, "ui_raycast_dump.txt");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[DumpUiRaycast] written to " + outPath);
        }

        [MenuItem("Tools/CoOp/Fix UI Button Raycast")]
        public static void FixUiButtonRaycast()
        {
            var log = new StringBuilder("[FixUiButtonRaycast]\n");
            foreach (var path in Prefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    log.AppendLine("=== " + path + " ===");
                    int buttons = 0, off = 0;
                    foreach (var btn in root.GetComponentsInChildren<Button>(true))
                    {
                        buttons++;
                        var own = btn.GetComponent<Graphic>();
                        if (own == null) own = btn.targetGraphic;
                        if (own == null)
                        {
                            log.AppendLine($"  WARN {btn.name}: no own graphic — skipped");
                            continue;
                        }

                        if (!own.raycastTarget) { own.raycastTarget = true; EditorUtility.SetDirty(own); }

                        foreach (var gr in btn.GetComponentsInChildren<Graphic>(true))
                        {
                            if (gr == own || !gr.raycastTarget) continue;
                            gr.raycastTarget = false;
                            EditorUtility.SetDirty(gr);
                            off++;
                            log.AppendLine($"  {btn.name}: off {gr.GetType().Name} @ {SubPath(gr.transform, btn.transform)}");
                        }
                        log.AppendLine($"  {btn.name}: keep ON {own.GetType().Name} (rect {((RectTransform)btn.transform).rect.width:F0}x{((RectTransform)btn.transform).rect.height:F0})");
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.AppendLine($"  -> buttons={buttons}, raycastOff={off}, saved.");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log(log.ToString());
        }

        private static string SubPath(Transform t, Transform stopAt)
        {
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null && p != stopAt) { sb.Insert(0, p.name + "/"); p = p.parent; }
            return sb.ToString();
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            string indent = new string(' ', depth * 2);

            string size = "";
            if (t is RectTransform rt)
            {
                var sd = rt.sizeDelta;
                var r = rt.rect;
                size = $" sd={sd.x:F0}x{sd.y:F0} rect={r.width:F0}x{r.height:F0}";
            }

            var graphic = t.GetComponent<Graphic>();
            string g = graphic != null
                ? $" [{graphic.GetType().Name} ray={(graphic.raycastTarget ? "ON" : "off")}]"
                : "";

            var sel = t.GetComponent<Selectable>();
            string s = sel != null ? $" <{sel.GetType().Name} interact={(sel.interactable ? 1 : 0)}>" : "";

            string a = t.GetComponent("UIButtonAnimator") != null ? " {ANIM}" : "";

            var cg = t.GetComponent<CanvasGroup>();
            string c = cg != null ? $" CG(block={(cg.blocksRaycasts ? 1 : 0)},int={(cg.interactable ? 1 : 0)})" : "";

            sb.AppendLine($"{indent}{t.name}{size}{g}{s}{a}{c}");
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1, sb);
        }
    }
}
#endif
