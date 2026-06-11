using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data.UI;
using Infrastructure.Services.Network;
using Infrastructure.Services.UI;
using Signals;

namespace Core.States
{
    public sealed class LobbyState : IState
    {
        private readonly IWindowService _windowService;
        private readonly IGameStateMachine _stateMachine;
        private readonly SignalBus _signalBus;
        private readonly ISessionService _session;

        private bool _starting;

        public LobbyState(IWindowService windowService,
                          IGameStateMachine stateMachine,
                          SignalBus signalBus,
                          ISessionService session)
        {
            _windowService = windowService;
            _stateMachine = stateMachine;
            _signalBus = signalBus;
            _session = session;
        }

        public UniTask EnterAsync(CancellationToken ct)
        {
            _starting = false;
            _signalBus.Subscribe<LobbyGameStartingSignal>(OnGameStarting);

            if (_session.IsServerOnly)
                return UniTask.CompletedTask;

            _signalBus.Subscribe<ConnectionLostSignal>(OnConnectionLost);
            _windowService.Open(WindowID.Room);
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken ct)
        {
            _signalBus.TryUnsubscribe<LobbyGameStartingSignal>(OnGameStarting);
            _signalBus.TryUnsubscribe<ConnectionLostSignal>(OnConnectionLost);
            if (!_session.IsServerOnly)
                _windowService.Close(WindowID.Room);
            return UniTask.CompletedTask;
        }

        private void OnGameStarting(LobbyGameStartingSignal _)
        {
            if (_starting) return;
            _starting = true;
            UnityEngine.Debug.Log("[Lobby] Game starting -> LoadGameState");
            _stateMachine.EnterAsync<LoadGameState>().Forget();
        }

        private void OnConnectionLost(ConnectionLostSignal _)
        {
            if (_starting) return;
            UnityEngine.Debug.Log("[Lobby] Connection lost -> LoadMainMenuState");
            _stateMachine.EnterAsync<LoadMainMenuState>().Forget();
        }
    }
}
