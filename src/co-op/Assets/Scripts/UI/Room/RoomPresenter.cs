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

            bool localIsHost = _lobby.IsHost;
            bool meReady = me?.Ready ?? false;
            bool otherReady = other?.Ready ?? false;
            string otherNick = other?.Nick;

            if (localIsHost)
            {
                _view.SetHostSlot(true, null);
                _view.SetClientSlot(false, other.HasValue, otherNick, otherReady);
            }
            else
            {
                _view.SetHostSlot(false, string.IsNullOrEmpty(otherNick) ? "Хост" : otherNick);
                _view.SetClientSlot(true, true, null, meReady);
            }

            _view.ShowReadyToggle(!localIsHost);
            if (!localIsHost) _view.SetLocalReady(meReady);

            _view.SetStartVisible(localIsHost, _lobby.CanStart);

            string status;
            if (localIsHost)
            {
                if (!other.HasValue) status = "Соло — можно начать (или ждите второго)";
                else if (otherReady) status = "Напарник готов — можно начинать";
                else status = "Ждём готовности напарника";
            }
            else
            {
                status = meReady ? "Готов — ждём запуска хостом" : "Отметьте готовность";
            }
            _view.SetStatus(status);
        }
    }
}
