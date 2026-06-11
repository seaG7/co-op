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
        private CancellationTokenSource _cts;

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
            _view.StartClicked += OnStart;
            _view.LeaveClicked += OnLeave;
            _signalBus.Subscribe<LobbyChangedSignal>(OnLobbyChanged);
            _lobby.RefreshLobby();
            Render();
            _cts = new CancellationTokenSource();
            WaitForLocalIdAsync(_cts.Token).Forget();
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _view.NicknameChanged -= OnNicknameChanged;
            _view.StartClicked -= OnStart;
            _view.LeaveClicked -= OnLeave;
            _signalBus.TryUnsubscribe<LobbyChangedSignal>(OnLobbyChanged);
        }

        private async UniTaskVoid WaitForLocalIdAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _lobby.LocalClientId < 0)
                await UniTask.Yield(PlayerLoopTiming.Update);
            if (!ct.IsCancellationRequested) Render();
        }

        private void OnNicknameChanged(string nick) => _lobby.SetLocalNickname(nick);

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

            LobbyMember? other = null;
            for (int i = 0; i < members.Length; i++)
                if (members[i].ClientId != localId && other == null) other = members[i];

            bool localIsLeader = _lobby.IsLeader;
            string otherNick = other?.Nick;

            if (localIsLeader)
            {
                _view.SetHostSlot(true, null);
                _view.SetClientSlot(false, other.HasValue, otherNick);
            }
            else
            {
                _view.SetHostSlot(false, string.IsNullOrEmpty(otherNick) ? "Создатель" : otherNick);
                _view.SetClientSlot(true, true, null);
            }

            _view.SetStartVisible(localIsLeader, _lobby.CanStart);

            string status;
            if (localIsLeader)
                status = other.HasValue ? "Второй игрок в комнате — можно начинать" : "Ожидание второго игрока…";
            else
                status = "Вы в комнате — ждём запуска создателем";
            _view.SetStatus(status);
        }
    }
}
