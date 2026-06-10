#if UNITY_EDITOR
using FishNet;
using Gameplay.Player.Vitals;
using Gameplay.World.Enemies;
using Gameplay.World.Round;
using Gameplay.World.Sources;
using Gameplay.World.Weapon;
using Infrastructure.Services.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Gameplay.DebugTools
{
    public sealed class DebugTestPanel : MonoBehaviour
    {
        private static DebugTestPanel _instance;

        private bool _open;
        private Rect _window = new Rect(8, 40, 320, 10);

        private CursorLockMode _savedLock;
        private bool _savedVisible;
        private IInputService _input;
        private bool _inputWasEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[DebugTestPanel]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DebugTestPanel>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
                Toggle();
        }

        private void Toggle()
        {
            _open = !_open;
            if (_open)
            {
                _savedLock = Cursor.lockState;
                _savedVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                var input = ResolveInput();
                _inputWasEnabled = input != null && input.IsEnabled;
                input?.Disable();
            }
            else
            {
                Cursor.lockState = _savedLock;
                Cursor.visible = _savedVisible;
                if (_inputWasEnabled) ResolveInput()?.Enable();
            }
        }

        private IInputService ResolveInput()
        {
            if (_input == null && ProjectContext.HasInstance)
                _input = ProjectContext.Instance.Container.TryResolve<IInputService>();
            return _input;
        }

        private void OnGUI()
        {
            if (!_open) return;
            _window = GUILayout.Window(0x0C00DE, _window, DrawWindow, "CO-OP DEBUG  —  F9 to close");
        }

        private void DrawWindow(int id)
        {
            bool isServer = InstanceFinder.IsServerStarted;
            var src = Source.All.Count > 0 ? Source.All[0] : null;
            var weapon = FindFirstObjectByType<Weapon>();
            var round = FindFirstObjectByType<RoundNetworkController>();

            GUILayout.Label(isServer ? "HOST — actions live" : "CLIENT — host-only actions no-op");
            GUILayout.Label($"Source: {(src != null ? src.State.Value.ToString() : "—")}    Mobs: {Enemy.All.Count}");
            if (weapon != null)
                GUILayout.Label($"Charge: {weapon.CorpsesLoaded.Value}/{weapon.RequiredCorpses}    Assembled: {weapon.IsAssembled}");

            Header("Mobs");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn 1") && src != null) src.ServerDebugSpawnOne();
            if (GUILayout.Button("Kill all") && src != null) src.ServerDebugDespawnEnemies();
            GUILayout.EndHorizontal();
            if (src != null)
            {
                bool paused = GUILayout.Toggle(src.DebugSpawnsPaused, " Pause auto-spawns");
                if (paused != src.DebugSpawnsPaused) src.DebugSpawnsPaused = paused;
            }

            Header("Source");
            if (GUILayout.Button("Destroy Source (instant)") && src != null) src.ServerDebugDestroy();

            Header("Cannon");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Assemble") && weapon != null) weapon.ServerDebugAssemble();
            if (GUILayout.Button("Charge full") && weapon != null) weapon.ServerDebugCharge();
            GUILayout.EndHorizontal();

            Header("Players");
            var players = PlayerVitals.All;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"P{p.OwnerId}: {p.State}", GUILayout.Width(110));
                if (GUILayout.Button("Down")) p.ServerKnockDown();
                if (GUILayout.Button("Revive")) p.ServerRevive();
                GUILayout.EndHorizontal();
            }

            Header("Round");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Win") && round != null) round.ServerDebugSetOutcome(Data.Rounds.RoundOutcome.Victory);
            if (GUILayout.Button("Force Lose") && round != null) round.ServerDebugSetOutcome(Data.Rounds.RoundOutcome.Defeat);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Time scale");
            Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0f, 3f);

            GUI.DragWindow();
        }

        private static void Header(string text)
        {
            GUILayout.Space(4);
            GUILayout.Label("— " + text + " —");
        }
    }
}
#endif
