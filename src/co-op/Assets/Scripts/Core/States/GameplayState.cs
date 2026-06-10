using System;
using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data.UI;
using Infrastructure.Services.Input;
using Infrastructure.Services.Network;
using Infrastructure.Services.Player;
using Infrastructure.Services.Scene;
using Infrastructure.Services.UI;
using Signals;
using UnityEngine;
using Zenject;

namespace Core.States
{
    public sealed class GameplayState : IState
    {
        private const float SpawnWatchdogSec = 15f;

        private readonly IGameStateMachine _stateMachine;
        private readonly IWindowService _windowService;
        private readonly IInputService _inputService;
        private readonly IPlayerService _playerService;
        private readonly ISessionService _session;
        private readonly ILoadingScreenService _loadingScreen;
        private readonly SignalBus _signalBus;

        private CancellationTokenSource _cts;
        private bool _subscribed;
        private bool _fallingBack;

        public GameplayState(
            IGameStateMachine stateMachine,
            IWindowService windowService,
            IInputService inputService,
            IPlayerService playerService,
            ISessionService session,
            ILoadingScreenService loadingScreen,
            SignalBus signalBus)
        {
            _stateMachine = stateMachine; _windowService = windowService;
            _inputService = inputService; _playerService = playerService;
            _session = session; _loadingScreen = loadingScreen; _signalBus = signalBus;
        }

        public async UniTask EnterAsync(CancellationToken ct)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var localCts = _cts;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Subscribe();
            _windowService.Open(WindowID.HUD);
            _inputService.Enable();

            using var watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(localCts.Token);
            FireWatchdogAsync(watchdogCts).Forget();

            try
            {
                await _playerService.WaitForLocalPlayerAsync(watchdogCts.Token);
                if (!watchdogCts.IsCancellationRequested) watchdogCts.Cancel();
                _loadingScreen.Hide();
            }
            catch (OperationCanceledException)
            {
                if (localCts.IsCancellationRequested) return;

                Debug.LogError("[GameplayState] LocalPlayer did not spawn within watchdog window.");
                FallbackToMenu();
            }
        }

        private static async UniTask FireWatchdogAsync(CancellationTokenSource cts)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(SpawnWatchdogSec), cancellationToken: cts.Token);
                if (!cts.IsCancellationRequested) cts.Cancel();
            }
            catch (OperationCanceledException) {  }
        }

        public UniTask ExitAsync(CancellationToken ct)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Unsubscribe();
            _inputService.Disable();
            _windowService.Close(WindowID.HUD);
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            return UniTask.CompletedTask;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _signalBus.Subscribe<ConnectionLostSignal>(OnConnectionLost);
            _signalBus.Subscribe<ConnectionFailedSignal>(OnConnectionFailed);
            _signalBus.Subscribe<SpawnFailedSignal>(OnSpawnFailed);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _signalBus.TryUnsubscribe<ConnectionLostSignal>(OnConnectionLost);
            _signalBus.TryUnsubscribe<ConnectionFailedSignal>(OnConnectionFailed);
            _signalBus.TryUnsubscribe<SpawnFailedSignal>(OnSpawnFailed);
            _subscribed = false;
        }

        private void OnConnectionLost(ConnectionLostSignal _) => FallbackToMenu();
        private void OnConnectionFailed(ConnectionFailedSignal _) => FallbackToMenu();

        private void OnSpawnFailed(SpawnFailedSignal s)
        {
            Debug.LogError($"[GameplayState] Spawn failed for client {s.ClientId}: {s.Reason}");
            if (s.ClientId == _session.LocalClientId)
                FallbackToMenu();
        }

        private void FallbackToMenu()
        {
            if (_fallingBack) return;
            _fallingBack = true;
            _loadingScreen.Hide();
            Unsubscribe();
            _cts?.Cancel();
            _stateMachine.EnterAsync<LoadMainMenuState>().Forget();
        }
    }
}
