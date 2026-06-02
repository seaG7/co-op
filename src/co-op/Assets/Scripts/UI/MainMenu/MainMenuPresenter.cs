using Core.StateMachine;
using Core.States;
using Cysharp.Threading.Tasks;
using Infrastructure.Services.Network;
using Infrastructure.Services.UI;
using UI.Common;
using UnityEngine;

namespace UI.MainMenu
{
    public sealed class MainMenuPresenter : IPresenter
    {
        private readonly MainMenuView _view;
        private readonly IGameStateMachine _stateMachine;
        private readonly IWindowService _windowService;
        private readonly ISessionService _session;

        public MainMenuPresenter(MainMenuView view,
                                 IGameStateMachine stateMachine,
                                 IWindowService windowService,
                                 ISessionService session)
        {
            _view = view; _stateMachine = stateMachine;
            _windowService = windowService; _session = session;
        }

        public void Initialize()
        {
            _view.HostClicked += OnHost;
            _view.ConnectClicked += OnConnect;
            _view.QuitClicked += OnQuit;
            _session.StateChanged += OnSessionState;
            OnSessionState(_session.State);
        }

        public void Dispose()
        {
            _view.HostClicked -= OnHost;
            _view.ConnectClicked -= OnConnect;
            _view.QuitClicked -= OnQuit;
            _session.StateChanged -= OnSessionState;
        }

        private void OnHost() => _stateMachine.EnterAsync<LoadGameState>().Forget();
        private void OnConnect() => _windowService.Open(WindowID.Connect);
        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnSessionState(SessionState s) =>
            _view.SetInteractable(s == SessionState.Disconnected || s == SessionState.Failed);
    }
}
