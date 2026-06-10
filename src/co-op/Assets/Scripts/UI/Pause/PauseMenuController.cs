using Data.UI;
using Infrastructure.Services.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace UI.Pause
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        private static PauseMenuController _instance;
        private IWindowService _windows;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[PauseMenu]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PauseMenuController>();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            var windows = Windows();
            if (windows == null) return;

            if (windows.IsWindowOpened(WindowID.Pause)) windows.Close(WindowID.Pause);
            else if (windows.IsWindowOpened(WindowID.HUD)) windows.Open(WindowID.Pause);
        }

        private IWindowService Windows()
        {
            if (_windows == null && ProjectContext.HasInstance)
                _windows = ProjectContext.Instance.Container.TryResolve<IWindowService>();
            return _windows;
        }
    }
}
