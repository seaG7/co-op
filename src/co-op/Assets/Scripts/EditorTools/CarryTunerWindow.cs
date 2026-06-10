#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Data.Configs;
using FishNet;
using Gameplay.Player.Animation;
using Gameplay.Player.Camera;
using Gameplay.Player.Carry;
using Gameplay.Player.Combat;
using Gameplay.World.Items;
using UnityEditor;
using UnityEngine;

namespace CoOp.EditorTools
{
    // Designer tool: tune how the player grabs/holds items (and the drink bottle) with Scene-view
    // Handles, in Play Mode (real hand IK), then bake into the prefab assets. Frames the player from
    // the side and isolates it on focus. Open: Tools/CoOp/Carry & Drink Tuner.
    public sealed class CarryTunerWindow : EditorWindow
    {
        private const string PlayerPath = "Assets/Prefabs/Player.prefab";
        private const string BottlePath = "Assets/Prefabs/World/Items/BottleDrinkable.prefab";

        private enum Mode { Carry, Bottle }
        private enum Target { Object, RightHand, LeftHand, RightElbow, LeftElbow, Anchor, DrinkAnchor }

        [MenuItem("Tools/CoOp/Carry & Drink Tuner")]
        public static void Open()
        {
            var w = GetWindow<CarryTunerWindow>("Carry Tuner");
            w.minSize = new Vector2(300, 380);
            w.Show();
        }

        private Mode _mode = Mode.Carry;
        private Target _active = Target.Object;
        private InteractableItemConfig[] _configs;
        private int _cfgIndex;
        private bool _isolated;
        private bool _autoFocusDone;

        private Transform _playerRoot;
        private PlayerCarry _carry;
        private PlayerDrink _drink;
        private PlayerAnimator _anim;

        private Carryable _held;
        private Drinkable _bottle;

        private readonly Stack<Snapshot> _undo = new();
        private readonly Stack<Snapshot> _redo = new();
        private Snapshot _preDrag;
        private bool _dragCommitted;

        private sealed class Pose { public bool Has; public Vector3 Pos; public Quaternion Rot; }

        private sealed class Snapshot
        {
            public bool HasHeld; public Vector3 HoldPos; public Vector3 HoldEuler;
            public Pose RHand, LHand, RElbow, LElbow, Anchor, DrinkAnchor;
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnScene;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            ReloadConfigs();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnScene;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            StopIsolation();
            ReleaseHeld();
            ReleaseBottle();
        }

        private void OnPlayModeChanged(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.ExitingPlayMode || s == PlayModeStateChange.EnteredEditMode)
            {
                StopIsolation();
                _held = null; _bottle = null; _autoFocusDone = false;
            }
        }

        private void ReloadConfigs()
        {
            _configs = AssetDatabase.FindAssets("t:InteractableItemConfig")
                .Select(g => AssetDatabase.LoadAssetAtPath<InteractableItemConfig>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && c.Prefab != null)
                .ToArray();
        }

        private bool ResolvePlayer()
        {
            var rig = PlayerCameraRig.Local;
            if (rig == null) { _playerRoot = null; _carry = null; return false; }
            if (_playerRoot != rig.transform)
            {
                _playerRoot = rig.transform;
                _carry = _playerRoot.GetComponent<PlayerCarry>();
                _drink = _playerRoot.GetComponent<PlayerDrink>();
                _anim = _playerRoot.GetComponent<PlayerAnimator>();
            }
            return _carry != null;
        }

        // ---------------------------------------------------------------- window GUI

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode as HOST in the Game scene, then tune with Scene-view handles.", MessageType.Info);
                if (GUILayout.Button("Enter Play Mode", GUILayout.Height(28))) EditorApplication.isPlaying = true;
                return;
            }
            if (!ResolvePlayer())
            {
                EditorGUILayout.HelpBox("No local player yet. Host and spawn into the Game scene.", MessageType.Warning);
                return;
            }
            HandleUndoCommands(Event.current);
            if (!InstanceFinder.IsServerStarted)
                EditorGUILayout.HelpBox("Spawn/Grab works on the HOST editor.", MessageType.Warning);

            if (!_autoFocusDone) { _autoFocusDone = true; FocusSide(); StartIsolation(); }

            var newMode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Carry item", "Bottle" });
            if (newMode != _mode) { _mode = newMode; _active = _mode == Mode.Carry ? Target.Object : Target.DrinkAnchor; }

            EditorGUILayout.Space(4);
            if (_mode == Mode.Carry) DrawCarrySource(); else DrawBottleSource();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Side")) FocusSide();
                if (GUILayout.Button("Front")) Focus(-_playerRoot.forward);
                if (GUILayout.Button("Back")) Focus(_playerRoot.forward);
                if (GUILayout.Button("Top")) Focus(Vector3.down);
            }
            bool iso = GUILayout.Toggle(_isolated, "  Isolate player + item (hide rest)");
            if (iso != _isolated) { if (iso) StartIsolation(); else StopIsolation(); }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Target — drag the handle in the Scene", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tool: W = move handle, E = rotate handle. Ctrl = snap.", EditorStyles.miniLabel);
            DrawTargetButtons();

            EditorGUILayout.Space(4);
            DrawActiveReadout();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_undo.Count == 0))
                    if (GUILayout.Button($"↶ Undo ({_undo.Count})")) DoUndo();
                using (new EditorGUI.DisabledScope(_redo.Count == 0))
                    if (GUILayout.Button($"↷ Redo ({_redo.Count})")) DoRedo();
            }

            EditorGUILayout.Space(8);
            var c = GUI.color; GUI.color = new Color(0.65f, 1f, 0.65f);
            if (GUILayout.Button("BAKE → prefab", GUILayout.Height(28))) BakeAll();
            GUI.color = c;
        }

        private void DrawCarrySource()
        {
            if (_configs == null || _configs.Length == 0)
            {
                EditorGUILayout.HelpBox("No InteractableItemConfig assets found.", MessageType.Warning);
                if (GUILayout.Button("Reload configs")) ReloadConfigs();
                return;
            }
            _cfgIndex = EditorGUILayout.Popup("Item", Mathf.Clamp(_cfgIndex, 0, _configs.Length - 1), _configs.Select(x => x.name).ToArray());
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Spawn & Grab", GUILayout.Height(24))) SpawnAndGrabCarry();
                if (GUILayout.Button("Release", GUILayout.Width(80), GUILayout.Height(24))) ReleaseHeld();
            }
            EditorGUILayout.LabelField(_held != null ? $"Held: {_held.name}  (two-hand: {_held.UsesTwoHands})" : "Held: —");
        }

        private void DrawBottleSource()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Spawn bottle + drink pose", GUILayout.Height(24))) SpawnBottleAndDrink();
                if (GUILayout.Button("Release", GUILayout.Width(80), GUILayout.Height(24))) ReleaseBottle();
            }
            EditorGUILayout.LabelField(_bottle != null ? "Drinking (anim held). Tune the Drink Anchor." : "Bottle: —");
        }

        private void DrawTargetButtons()
        {
            var targets = ActiveTargets();
            int idx = Mathf.Max(0, targets.IndexOf(_active));
            string[] names = targets.Select(Name).ToArray();
            int sel = GUILayout.SelectionGrid(idx, names, Mathf.Min(3, names.Length));
            if (sel != idx) { _active = targets[sel]; SceneView.RepaintAll(); }

            if (_mode == Mode.Carry && _held != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Mirror R → L")) Mirror(HandSide.Right, HandSide.Left);
                    if (GUILayout.Button("Mirror L → R")) Mirror(HandSide.Left, HandSide.Right);
                }
            }
        }

        private void DrawActiveReadout()
        {
            var before = Capture();
            if (_active == Target.Object)
            {
                if (_held == null) return;
                EditorGUI.BeginChangeCheck();
                Vector3 p = EditorGUILayout.Vector3Field("Hold Pos", _held.HoldPositionOffset);
                Vector3 e = EditorGUILayout.Vector3Field("Hold Euler", _held.HoldEulerOffset);
                if (EditorGUI.EndChangeCheck()) { PushSnapshot(before); _held.EditorSetHoldPose(p, e); }
                if (GUILayout.Button("Reset hold pose")) { PushSnapshot(before); _held.EditorSetHoldPose(Vector3.zero, Vector3.zero); }
                return;
            }
            var t = GetTargetTransform(_active, false);
            if (t == null) { EditorGUILayout.LabelField("(drag in the Scene to create this handle)"); return; }
            EditorGUI.BeginChangeCheck();
            Vector3 lp = EditorGUILayout.Vector3Field("Local Pos", t.localPosition);
            Vector3 le = HasRotation(_active) ? EditorGUILayout.Vector3Field("Local Euler", t.localEulerAngles) : Vector3.zero;
            if (EditorGUI.EndChangeCheck()) { PushSnapshot(before); t.localPosition = lp; if (HasRotation(_active)) t.localEulerAngles = le; }
            if (GUILayout.Button("Reset target")) { PushSnapshot(before); t.localPosition = Vector3.zero; if (HasRotation(_active)) t.localRotation = Quaternion.identity; }
        }

        // ---------------------------------------------------------------- scene handles

        private void OnScene(SceneView sv)
        {
            if (!EditorApplication.isPlaying || !ResolvePlayer()) return;
            var e = Event.current;
            HandleUndoCommands(e);
            if (e.type == EventType.MouseDown && e.button == 0) { _preDrag = Capture(); _dragCommitted = false; }
            foreach (var t in ActiveTargets())
            {
                if (t == _active) DrawActiveHandle(t);
                else DrawMarker(t);
            }
        }

        private void DrawActiveHandle(Target t)
        {
            if (t == Target.Object)
            {
                if (_held == null || _carry == null || _carry.CarryAnchor == null) return;
                var anchor = _carry.CarryAnchor;
                var tr = _held.transform;
                Handles.color = Col(t);
                Handles.Label(tr.position + Vector3.up * 0.06f, "Object (active)");
                EditorGUI.BeginChangeCheck();
                Vector3 p = tr.position; Quaternion r = tr.rotation;
                if (Tools.current == Tool.Rotate) r = Handles.RotationHandle(r, p);
                else p = Handles.PositionHandle(p, r);
                if (EditorGUI.EndChangeCheck())
                {
                    CommitDrag();
                    _held.EditorSetHoldPose(anchor.InverseTransformPoint(p),
                        (Quaternion.Inverse(anchor.rotation) * r).eulerAngles);
                    Repaint();
                }
                return;
            }

            var x = GetTargetTransform(t, true);
            if (x == null) return;
            Handles.color = Col(t);
            Handles.Label(x.position + Vector3.up * 0.06f, Name(t) + " (active)");
            EditorGUI.BeginChangeCheck();
            if (HasRotation(t) && Tools.current == Tool.Rotate)
            {
                Quaternion r = Handles.RotationHandle(x.rotation, x.position);
                if (EditorGUI.EndChangeCheck()) { CommitDrag(); x.rotation = r; Repaint(); }
            }
            else
            {
                Quaternion basis = HasRotation(t) ? x.rotation : Quaternion.identity;
                Vector3 p = Handles.PositionHandle(x.position, basis);
                if (EditorGUI.EndChangeCheck()) { CommitDrag(); x.position = p; Repaint(); }
            }
        }

        private void DrawMarker(Target t)
        {
            Vector3 p;
            if (t == Target.Object) { if (_held == null) return; p = _held.transform.position; }
            else { var tr = GetTargetTransform(t, false); if (tr == null) return; p = tr.position; }

            Handles.color = Col(t);
            float h = HandleUtility.GetHandleSize(p) * 0.13f;
            if (Handles.Button(p, Quaternion.identity, h, h, Handles.SphereHandleCap)) { _active = t; Repaint(); }
            Handles.Label(p + Vector3.up * 0.045f, Name(t));
        }

        // ---------------------------------------------------------------- targets

        private List<Target> ActiveTargets()
        {
            if (_mode == Mode.Bottle) return new List<Target> { Target.DrinkAnchor };
            var list = new List<Target> { Target.Object, Target.RightHand, Target.RightElbow, Target.Anchor };
            if (_held == null || _held.UsesTwoHands) list.InsertRange(2, new[] { Target.LeftHand, Target.LeftElbow });
            return list;
        }

        private Transform GetTargetTransform(Target t, bool create)
        {
            switch (t)
            {
                case Target.RightHand: return Grip(HandSide.Right, create);
                case Target.LeftHand: return Grip(HandSide.Left, create);
                case Target.RightElbow: return Elbow(HandSide.Right, create);
                case Target.LeftElbow: return Elbow(HandSide.Left, create);
                case Target.Anchor: return _carry != null ? _carry.CarryAnchor : null;
                case Target.DrinkAnchor: return _drink != null ? _drink.DrinkAnchor : null;
                default: return null;
            }
        }

        private Transform Grip(HandSide s, bool create)
        {
            if (_held == null) return null;
            var g = _held.RawGrip(s);
            if (g == null && create) { g = NewChild(_held.transform, s == HandSide.Right ? "RightHandGrip" : "LeftHandGrip"); _held.EditorSetGrip(s, g); }
            return g;
        }

        private Transform Elbow(HandSide s, bool create)
        {
            if (_held == null) return null;
            var e = _held.ElbowHint(s);
            if (e == null && create)
            {
                e = NewChild(_held.transform, s == HandSide.Right ? "RightElbowHint" : "LeftElbowHint");
                e.localPosition = new Vector3(s == HandSide.Right ? 0.3f : -0.3f, -0.15f, -0.25f);
                _held.EditorSetElbowHint(s, e);
            }
            return e;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private void Mirror(HandSide from, HandSide to)
        {
            if (_held == null) return;
            PushSnapshot(Capture());
            var fg = _held.RawGrip(from);
            if (fg != null) { var tg = Grip(to, true); var lp = fg.localPosition; lp.x = -lp.x; tg.localPosition = lp; var e = fg.localEulerAngles; tg.localRotation = Quaternion.Euler(e.x, -e.y, -e.z); }
            var fe = _held.ElbowHint(from);
            if (fe != null) { var te = Elbow(to, true); var lp = fe.localPosition; lp.x = -lp.x; te.localPosition = lp; }
            SceneView.RepaintAll();
        }

        private static bool HasRotation(Target t) => t != Target.RightElbow && t != Target.LeftElbow;

        private static string Name(Target t) => t switch
        {
            Target.Object => "Object", Target.RightHand => "R.Hand", Target.LeftHand => "L.Hand",
            Target.RightElbow => "R.Elbow", Target.LeftElbow => "L.Elbow", Target.Anchor => "Anchor",
            Target.DrinkAnchor => "Drink Anchor", _ => t.ToString()
        };

        private static Color Col(Target t) => t switch
        {
            Target.Object => new Color(1f, 0.85f, 0.2f), Target.RightHand => new Color(0.3f, 1f, 0.4f),
            Target.LeftHand => new Color(0.3f, 0.7f, 1f), Target.RightElbow => new Color(0.6f, 1f, 0.7f),
            Target.LeftElbow => new Color(0.6f, 0.85f, 1f), Target.Anchor => new Color(1f, 0.5f, 0.9f),
            _ => new Color(1f, 0.7f, 0.3f)
        };

        // ---------------------------------------------------------------- spawn / grab

        private void SpawnAndGrabCarry()
        {
            if (!InstanceFinder.IsServerStarted || _carry == null || _configs == null || _configs.Length == 0) return;
            ReleaseHeld();
            var config = _configs[Mathf.Clamp(_cfgIndex, 0, _configs.Length - 1)];
            if (config == null || config.Prefab == null) return;
            Vector3 pos = _carry.CarryAnchor != null ? _carry.CarryAnchor.position : _playerRoot.position + _playerRoot.forward;
            var go = Instantiate(config.Prefab, pos, _playerRoot.rotation);
            InstanceFinder.ServerManager.Spawn(go, null);
            _held = go.GetComponent<Carryable>();
            if (_held != null) _held.HolderClientId.Value = _carry.OwnerId;
            if (_active != Target.Object && !ActiveTargets().Contains(_active)) _active = Target.Object;
            StartIsolation();
            SceneView.RepaintAll();
        }

        private void SpawnBottleAndDrink()
        {
            if (!InstanceFinder.IsServerStarted || _drink == null) return;
            ReleaseBottle();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BottlePath);
            if (prefab == null) { Debug.LogWarning("[CarryTuner] BottleDrinkable.prefab not found."); return; }
            Vector3 pos = _drink.DrinkAnchor != null ? _drink.DrinkAnchor.position : _playerRoot.position + Vector3.up;
            var go = Instantiate(prefab, pos, Quaternion.identity);
            InstanceFinder.ServerManager.Spawn(go, null);
            _bottle = go.GetComponent<Drinkable>();
            if (_bottle != null) _bottle.DrinkerClientId.Value = _drink.OwnerId;
            _anim?.SetDrinking(true);
            StartIsolation();
            SceneView.RepaintAll();
        }

        private void ReleaseHeld()
        {
            if (_held == null) return;
            if (InstanceFinder.IsServerStarted)
            {
                _held.HolderClientId.Value = -1;
                if (_held.NetworkObject != null) InstanceFinder.ServerManager.Despawn(_held.NetworkObject);
            }
            _held = null;
        }

        private void ReleaseBottle()
        {
            _anim?.SetDrinking(false);
            if (_bottle == null) return;
            if (InstanceFinder.IsServerStarted)
            {
                _bottle.DrinkerClientId.Value = -1;
                if (_bottle.NetworkObject != null) InstanceFinder.ServerManager.Despawn(_bottle.NetworkObject);
            }
            _bottle = null;
        }

        // ---------------------------------------------------------------- view / isolation

        private void FocusSide() { if (_playerRoot != null) Focus(-_playerRoot.right); }

        private void Focus(Vector3 camForward)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null || _playerRoot == null) return;
            Vector3 pivot = _carry != null && _carry.CarryAnchor != null ? _carry.CarryAnchor.position : _playerRoot.position + Vector3.up * 1.3f;
            sv.LookAt(pivot, Quaternion.LookRotation(camForward.normalized, Vector3.up), 1.2f);
            sv.Repaint();
        }

        private void StartIsolation()
        {
            if (_playerRoot == null) return;
            var set = new List<GameObject> { _playerRoot.gameObject };
            if (_held != null) set.Add(_held.gameObject);
            if (_bottle != null) set.Add(_bottle.gameObject);
            SceneVisibilityManager.instance.Isolate(set.ToArray(), true);
            _isolated = true;
        }

        private void StopIsolation()
        {
            if (!_isolated) return;
            SceneVisibilityManager.instance.ExitIsolation();
            _isolated = false;
        }

        // ---------------------------------------------------------------- bake

        private void BakeAll()
        {
            if (_mode == Mode.Carry)
            {
                if (_held != null && _cfgIndex >= 0 && _cfgIndex < _configs.Length)
                    BakeCarryable(_held, _configs[_cfgIndex].Prefab);
                if (_carry != null && _carry.CarryAnchor != null)
                    BakeChildOnPrefab(PlayerPath, "CarryAnchor", _carry.CarryAnchor);
            }
            else if (_drink != null && _drink.DrinkAnchor != null)
            {
                BakeChildOnPrefab(PlayerPath, "DrinkAnchor", _drink.DrinkAnchor);
            }
        }

        private static void BakeCarryable(Carryable live, GameObject prefabAsset)
        {
            if (live == null || prefabAsset == null) return;
            string path = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("[CarryTuner] no asset path for prefab."); return; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var c = root.GetComponent<Carryable>();
                if (c == null) { Debug.LogWarning($"[CarryTuner] {path} has no Carryable."); return; }
                c.EditorSetHoldPose(live.HoldPositionOffset, live.HoldEulerOffset);
                BakeChild(root.transform, live.RawGrip(HandSide.Left), t => c.EditorSetGrip(HandSide.Left, t));
                BakeChild(root.transform, live.RawGrip(HandSide.Right), t => c.EditorSetGrip(HandSide.Right, t));
                BakeChild(root.transform, live.ElbowHint(HandSide.Left), t => c.EditorSetElbowHint(HandSide.Left, t));
                BakeChild(root.transform, live.ElbowHint(HandSide.Right), t => c.EditorSetElbowHint(HandSide.Right, t));
                EditorUtility.SetDirty(c);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[CarryTuner] baked hold pose + grips/elbows → {path}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BakeChild(Transform prefabRoot, Transform live, System.Action<Transform> wire)
        {
            if (live == null) return;
            var child = FindDirect(prefabRoot, live.name) ?? NewChild(prefabRoot, live.name);
            child.localPosition = live.localPosition;
            child.localRotation = live.localRotation;
            wire(child);
        }

        private static void BakeChildOnPrefab(string prefabPath, string childName, Transform live)
        {
            if (live == null) return;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var t = FindDeep(root.transform, childName);
                if (t == null) { Debug.LogWarning($"[CarryTuner] '{childName}' not found on {prefabPath}."); return; }
                t.localPosition = live.localPosition;
                t.localRotation = live.localRotation;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[CarryTuner] baked {childName} → {prefabPath}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform FindDirect(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ---------------------------------------------------------------- undo / redo

        private void HandleUndoCommands(Event e)
        {
            if (e == null) return;
            bool isUndo = e.commandName == "Undo";
            bool isRedo = e.commandName == "Redo";
            if (!isUndo && !isRedo) return;
            if ((isUndo && _undo.Count == 0) || (isRedo && _redo.Count == 0)) return;
            if (e.type == EventType.ValidateCommand) e.Use();
            else if (e.type == EventType.ExecuteCommand) { if (isUndo) DoUndo(); else DoRedo(); e.Use(); }
        }

        private void PushSnapshot(Snapshot s)
        {
            if (s == null) return;
            _undo.Push(s);
            _redo.Clear();
        }

        private void CommitDrag()
        {
            if (_dragCommitted || _preDrag == null) return;
            PushSnapshot(_preDrag);
            _dragCommitted = true;
        }

        private void DoUndo()
        {
            if (_undo.Count == 0) return;
            _redo.Push(Capture());
            Apply(_undo.Pop());
            Repaint();
            SceneView.RepaintAll();
        }

        private void DoRedo()
        {
            if (_redo.Count == 0) return;
            _undo.Push(Capture());
            Apply(_redo.Pop());
            Repaint();
            SceneView.RepaintAll();
        }

        private Snapshot Capture()
        {
            var s = new Snapshot();
            if (_held != null) { s.HasHeld = true; s.HoldPos = _held.HoldPositionOffset; s.HoldEuler = _held.HoldEulerOffset; }
            s.RHand = CapT(GetTargetTransform(Target.RightHand, false));
            s.LHand = CapT(GetTargetTransform(Target.LeftHand, false));
            s.RElbow = CapT(GetTargetTransform(Target.RightElbow, false));
            s.LElbow = CapT(GetTargetTransform(Target.LeftElbow, false));
            s.Anchor = CapT(GetTargetTransform(Target.Anchor, false));
            s.DrinkAnchor = CapT(GetTargetTransform(Target.DrinkAnchor, false));
            return s;
        }

        private void Apply(Snapshot s)
        {
            if (s == null) return;
            if (s.HasHeld && _held != null) _held.EditorSetHoldPose(s.HoldPos, s.HoldEuler);
            ApplyT(GetTargetTransform(Target.RightHand, false), s.RHand);
            ApplyT(GetTargetTransform(Target.LeftHand, false), s.LHand);
            ApplyT(GetTargetTransform(Target.RightElbow, false), s.RElbow);
            ApplyT(GetTargetTransform(Target.LeftElbow, false), s.LElbow);
            ApplyT(GetTargetTransform(Target.Anchor, false), s.Anchor);
            ApplyT(GetTargetTransform(Target.DrinkAnchor, false), s.DrinkAnchor);
        }

        private static Pose CapT(Transform t)
            => t == null ? new Pose { Has = false } : new Pose { Has = true, Pos = t.localPosition, Rot = t.localRotation };

        private static void ApplyT(Transform t, Pose p)
        {
            if (t == null || p == null || !p.Has) return;
            t.localPosition = p.Pos;
            t.localRotation = p.Rot;
        }
    }
}
#endif
