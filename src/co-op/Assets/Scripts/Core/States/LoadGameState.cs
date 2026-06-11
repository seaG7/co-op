using System;
using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data.Paths;
using FishNet.Managing.Scened;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Network;
using Infrastructure.Services.Scene;
using Signals;
using UnityEngine;

namespace Core.States
{
    public sealed class LoadGameState : IState
    {
        private readonly IGameStateMachine _stateMachine;
        private readonly ISessionService _session;
        private readonly INetworkService _network;
        private readonly ILoadingScreenService _loadingScreen;
        private readonly IConfigDataProvider _configs;
        private readonly SignalBus _signalBus;

        public LoadGameState(
            IGameStateMachine stateMachine,
            ISessionService session,
            INetworkService network,
            ILoadingScreenService loadingScreen,
            IConfigDataProvider configs,
            SignalBus signalBus)
        {
            _stateMachine = stateMachine; _session = session; _network = network;
            _loadingScreen = loadingScreen; _configs = configs; _signalBus = signalBus;
        }

        public async UniTask EnterAsync(CancellationToken ct)
        {
            bool showUi = !_session.IsServerOnly;
            if (showUi) _loadingScreen.Show();
            try
            {
                if (_session.State == SessionState.Disconnected || _session.State == SessionState.Failed)
                {
                    var port = _configs?.Network?.DefaultPort ?? (ushort)7777;
                    var ok = await _session.StartHostAsync(port, ct);
                    if (!ok)
                    {
                        Debug.LogError($"[LoadGameState] StartHost failed: {_session.LastError}");
                        if (showUi) _loadingScreen.Hide();
                        await _stateMachine.EnterAsync<LoadMainMenuState>(CancellationToken.None);
                        return;
                    }
                }

                if (showUi) _loadingScreen.SetProgress(0.1f);

                var levelTcs = new UniTaskCompletionSource();
                void OnLevelReady(LevelReadySignal _) => levelTcs.TrySetResult();
                void OnScenePercent(SceneLoadPercentEventArgs a)
                {
                    if (showUi) _loadingScreen.SetProgress(0.1f + 0.6f * Mathf.Clamp01(a.Percent));
                }

                _signalBus.Subscribe<LevelReadySignal>(OnLevelReady);
                var sceneManager = _network.NetworkManager != null ? _network.NetworkManager.SceneManager : null;
                if (sceneManager != null) sceneManager.OnLoadPercentChange += OnScenePercent;
                try
                {
                    if (_network.IsServer)
                        await _network.LoadGlobalSceneAsync(ScenePaths.GAME_SCENE, ct);
                    else
                        await _network.WaitForSceneLoadedAsync(ScenePaths.GAME_SCENE, ct);

                    if (showUi) _loadingScreen.SetProgress(0.7f);

                    await levelTcs.Task
                        .AttachExternalCancellation(ct)
                        .Timeout(TimeSpan.FromSeconds(60));

                    if (showUi) _loadingScreen.SetProgress(0.9f);
                }
                catch (TimeoutException)
                {
                    Debug.LogError("[LoadGameState] Level-ready timed out.");
                    if (showUi) _loadingScreen.Hide();
                    await _session.LeaveAsync(CancellationToken.None);
                    await _stateMachine.EnterAsync<LoadMainMenuState>(CancellationToken.None);
                    return;
                }
                finally
                {
                    _signalBus.TryUnsubscribe<LevelReadySignal>(OnLevelReady);
                    if (sceneManager != null) sceneManager.OnLoadPercentChange -= OnScenePercent;
                }
            }
            catch (OperationCanceledException)
            {
                if (showUi) _loadingScreen.Hide();
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoadGameState] {ex}");
                if (showUi) _loadingScreen.Hide();
                await _session.LeaveAsync(CancellationToken.None);
                await _stateMachine.EnterAsync<LoadMainMenuState>(CancellationToken.None);
                return;
            }

            await _stateMachine.EnterAsync<GameplayState>(ct);
        }

        public UniTask ExitAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
