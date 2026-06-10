using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data.UI;
using Infrastructure.Providers.Configs;
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
        private readonly IConfigDataProvider _configs;

        private bool _starting;
        private bool _migrating;

        public LobbyState(IWindowService windowService,
                          IGameStateMachine stateMachine,
                          SignalBus signalBus,
                          ISessionService session,
                          IConfigDataProvider configs)
        {
            _windowService = windowService;
            _stateMachine = stateMachine;
            _signalBus = signalBus;
            _session = session;
            _configs = configs;
        }

        public UniTask EnterAsync(CancellationToken ct)
        {
            _starting = false;
            _migrating = false;
            _signalBus.Subscribe<LobbyGameStartingSignal>(OnGameStarting);
            _signalBus.Subscribe<ConnectionLostSignal>(OnConnectionLost);
            _windowService.Open(WindowID.Room);
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken ct)
        {
            _signalBus.TryUnsubscribe<LobbyGameStartingSignal>(OnGameStarting);
            _signalBus.TryUnsubscribe<ConnectionLostSignal>(OnConnectionLost);
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

        private async void OnConnectionLost(ConnectionLostSignal _)
        {
            if (_starting || _migrating) return;
            _migrating = true;

            var port = _configs?.Network != null ? _configs.Network.DefaultPort : (ushort)7777;
            await _session.LeaveAsync(CancellationToken.None);
            var ok = await _session.StartHostAsync(port);

            _migrating = false;
            if (!ok) _stateMachine.EnterAsync<LoadMainMenuState>().Forget();
        }
    }
}
