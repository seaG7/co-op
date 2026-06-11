using System;
using Core.StateMachine;
using Core.States;
using Cysharp.Threading.Tasks;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Network;
using UnityEngine;
using Zenject;

namespace Bootstrap
{
    public class EntryPoint : MonoBehaviour
    {
        [Inject] private IGameStateMachine _stateMachine;
        [Inject] private ISessionService _session;
        [Inject] private GameStateMachine _concreteStateMachine;
        [Inject] private IConfigDataProvider _configs;

        private async void Start()
        {
            try
            {
                EnsureInjected();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EntryPoint] Failed to resolve dependencies from ProjectContext. " +
                               $"Verify Assets/Resources/ProjectContext.prefab has the ProjectInstaller component attached " +
                               $"and listed in the ProjectContext.MonoInstallers list. Error: {ex}");
                return;
            }

            _concreteStateMachine.OnEnterFailed = () =>
            {
                if (_session.IsServerOnly) return;
                var current = _stateMachine.CurrentState?.GetType().Name;
                if (current == nameof(LoadMainMenuState) || current == nameof(MainMenuState) || current == nameof(BootstrapState))
                    return;
                _stateMachine.EnterAsync<LoadMainMenuState>().Forget();
            };

            await _stateMachine.EnterAsync<BootstrapState>();

#if UNITY_EDITOR
            await HandleEditorAutoLaunch();
#endif
        }

        private void EnsureInjected()
        {
            if (_stateMachine != null && _session != null && _concreteStateMachine != null && _configs != null) return;

            var container = ProjectContext.Instance.Container;
            container.Inject(this);

            if (_stateMachine == null || _session == null || _concreteStateMachine == null || _configs == null)
                throw new InvalidOperationException(
                    "EntryPoint [Inject] fields are still null after manual injection. " +
                    "Check that ProjectInstaller is bound on the ProjectContext prefab.");
        }

#if UNITY_EDITOR
        private async UniTask HandleEditorAutoLaunch()
        {
            var mode = UnityEditor.EditorPrefs.GetString("CoOp.LaunchMode", string.Empty);
            UnityEditor.EditorPrefs.DeleteKey("CoOp.LaunchMode");
            if (string.IsNullOrEmpty(mode)) return;

            if (mode == "Host")
            {
                await _stateMachine.EnterAsync<LoadGameState>();
            }
            else if (mode == "Client")
            {
                var net = _configs?.Network;
                if (net == null)
                {
                    Debug.LogError("[EntryPoint] NetworkConfig not loaded via IConfigDataProvider; cannot auto-join.");
                    return;
                }
                var ok = await _session.JoinAsync(net.DefaultAddress, net.DefaultPort);
                if (ok) await _stateMachine.EnterAsync<LoadGameState>();
            }
            else if (mode == "Server")
            {
                var net = _configs?.Network;
                var port = net != null ? net.DefaultPort : (ushort)7777;
                var ok = await _session.StartServerOnlyAsync(port);
                if (ok) await _stateMachine.EnterAsync<LobbyState>();
            }
        }
#endif
    }
}
