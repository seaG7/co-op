using Core.StateMachine;
using Core.States;
using Cysharp.Threading.Tasks;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Network;
using UI.Common;
using UnityEngine;

namespace UI.MainMenu
{
    public sealed class MainMenuPresenter : IPresenter
    {
        private readonly MainMenuView _view;
        private readonly IGameStateMachine _stateMachine;
        private readonly ISessionService _session;
        private readonly IConfigDataProvider _configs;

        public MainMenuPresenter(MainMenuView view,
                                 IGameStateMachine stateMachine,
                                 ISessionService session,
                                 IConfigDataProvider configs)
        {
            _view = view;
            _stateMachine = stateMachine;
            _session = session;
            _configs = configs;
        }

        public void Initialize()
        {
            _view.HostClicked += OnHost;
            _view.ConnectClicked += OnJoin;
            _view.QuitClicked += OnQuit;
            _session.StateChanged += OnSessionState;
            OnSessionState(_session.State);
        }

        public void Dispose()
        {
            _view.HostClicked -= OnHost;
            _view.ConnectClicked -= OnJoin;
            _view.QuitClicked -= OnQuit;
            _session.StateChanged -= OnSessionState;
        }

        private async void OnHost()
        {
            var net = _configs?.Network;
            var port = net != null ? net.DefaultPort : (ushort)7777;
            bool dedicated = net != null && net.UseDedicatedServer;

            _view.SetStatus("Создание комнаты…");
            var ok = dedicated
                ? await _session.JoinAsync(net.DefaultAddress, port)
                : await _session.StartHostAsync(port);

            if (!ok) { _view.SetStatus(_session.LastError ?? "Не удалось создать комнату"); return; }
            EnterGame(dedicated);
        }

        private async void OnJoin()
        {
            var net = _configs?.Network;
            var address = net != null ? net.DefaultAddress : "127.0.0.1";
            var port = net != null ? net.DefaultPort : (ushort)7777;

            _view.SetStatus("Поиск комнаты…");
            var ok = await _session.JoinAsync(address, port);
            if (!ok) { _view.SetStatus(_session.LastError ?? "Не удалось подключиться"); return; }
            EnterGame(net != null && net.UseDedicatedServer);
        }

        private void EnterGame(bool dedicated)
        {
            if (dedicated) _stateMachine.EnterAsync<LobbyState>().Forget();
            else _stateMachine.EnterAsync<LoadGameState>().Forget();
        }

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
