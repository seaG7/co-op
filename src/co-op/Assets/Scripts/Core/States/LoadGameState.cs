using System;
using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data.Paths;
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
            _loadingScreen.Show();
            try
            {
                if (_session.State == SessionState.Disconnected || _session.State == SessionState.Failed)
                {
                    var port = _configs?.Network?.DefaultPort ?? (ushort)7777;
                    var ok = await _session.StartHostAsync(port, ct);
                    if (!ok)
                    {
                        Debug.LogError($"[LoadGameState] StartHost failed: {_session.LastError}");
                        await _stateMachine.EnterAsync<LoadMainMenuState>(CancellationToken.None);
                        return;
                    }
                }

                var levelTcs = new UniTaskCompletionSource();
                void OnLevelReady(LevelReadySignal _) => levelTcs.TrySetResult();
                _signalBus.Subscribe<LevelReadySignal>(OnLevelReady);
                try
                {
                    if (_network.IsServer)
                        await _network.LoadGlobalSceneAsync(ScenePaths.GAME_SCENE, ct);
                    else
                        await _network.WaitForSceneLoadedAsync(ScenePaths.GAME_SCENE, ct);

                    await levelTcs.Task
                        .AttachExternalCancellation(ct)
                        .Timeout(TimeSpan.FromSeconds(60));
                }
                catch (TimeoutException)
                {
                    Debug.LogError("[LoadGameState] Level-ready timed out.");
                    await _session.LeaveAsync(CancellationToken.None);
                    await _stateMachine.EnterAsync<LoadMainMenuState>(CancellationToken.None);
                    return;
                }
                finally
                {
                    _signalBus.TryUnsubscribe<LevelReadySignal>(OnLevelReady);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Debug.LogError($"[LoadGameState] {ex}");
                await _session.LeaveAsync(CancellationToken.None);
                await _stateMachine.EnterAsync<LoadMainMenuState>(CancellationToken.None);
                return;
            }
            finally
            {
                _loadingScreen.Hide();
            }

            await _stateMachine.EnterAsync<GameplayState>(ct);
        }

        public UniTask ExitAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
