using System;
using System.Threading;
using Core.StateMachine;
using Core.States;
using Cysharp.Threading.Tasks;
using Infrastructure.Services.Lobby;
using Infrastructure.Services.Network;
using Signals;
using UI.Common;

namespace UI.Room
{
    public sealed class RoomPresenter : IPresenter
    {
        private readonly RoomView _view;
        private readonly ILobbyService _lobby;
        private readonly ISessionService _session;
        private readonly IGameStateMachine _stateMachine;
        private readonly SignalBus _signalBus;

        public RoomPresenter(RoomView view,
                             ILobbyService lobby,
                             ISessionService session,
                             IGameStateMachine stateMachine,
                             SignalBus signalBus)
        {
            _view = view;
            _lobby = lobby;
            _session = session;
            _stateMachine = stateMachine;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            _view.NicknameChanged += OnNicknameChanged;
            _view.ReadyChanged += OnReadyChanged;
            _view.StartClicked += OnStart;
            _view.LeaveClicked += OnLeave;
            _signalBus.Subscribe<LobbyChangedSignal>(OnLobbyChanged);
            _lobby.RefreshLobby();
            Render();
        }

        public void Dispose()
        {
            _view.NicknameChanged -= OnNicknameChanged;
            _view.ReadyChanged -= OnReadyChanged;
            _view.StartClicked -= OnStart;
            _view.LeaveClicked -= OnLeave;
            _signalBus.TryUnsubscribe<LobbyChangedSignal>(OnLobbyChanged);
        }

        private void OnNicknameChanged(string nick) => _lobby.SetLocalNickname(nick);
        private void OnReadyChanged(bool ready) => _lobby.SetLocalReady(ready);

        private void OnStart()
        {
            UnityEngine.Debug.Log("[Room] Start clicked");
            _lobby.StartGame();
        }

        private void OnLeave()
        {
            _session.LeaveAsync(CancellationToken.None).Forget();
            _stateMachine.EnterAsync<LoadMainMenuState>().Forget();
        }

        private void OnLobbyChanged(LobbyChangedSignal _) => Render();

        private void Render()
        {
            var members = _lobby.Members ?? Array.Empty<LobbyMember>();
            int localId = _lobby.LocalClientId;

            LobbyMember? me = null;
            LobbyMember? other = null;
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].ClientId == localId) me = members[i];
                else if (other == null) other = members[i];
            }

            if (me.HasValue) _view.SetLocalReady(me.Value.Ready);

            bool hasOther = other.HasValue;
            _view.ShowRemote(hasOther);
            if (hasOther) _view.SetRemote(other.Value.Nick, other.Value.Ready);

            bool host = _lobby.IsHost;
            _view.SetStartVisible(host, _lobby.CanStart);

            int count = members.Length;
            string status;
            if (count == 0) status = "Подключение…";
            else if (count == 1) status = "Соло — можно начать (или ждите второго)  1/2";
            else if (_lobby.AllReady) status = $"Готовы {count}/2 — можно начинать";
            else status = $"Игроки {count}/2 — ждём готовности";
            _view.SetStatus(status);
        }
    }
}
