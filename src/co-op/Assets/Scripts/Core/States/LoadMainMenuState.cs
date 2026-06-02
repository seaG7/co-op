using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data.Paths;
using Infrastructure.Services.Network;
using Infrastructure.Services.Scene;
using UnityEngine.SceneManagement;

namespace Core.States
{
    public sealed class LoadMainMenuState : IState
    {
        private readonly IGameStateMachine _stateMachine;
        private readonly ISceneLoaderService _sceneLoader;
        private readonly ISessionService _session;

        public LoadMainMenuState(IGameStateMachine sm, ISceneLoaderService sceneLoader, ISessionService session)
        {
            _stateMachine = sm; _sceneLoader = sceneLoader; _session = session;
        }

        public async UniTask EnterAsync(CancellationToken ct)
        {
            if (_session.State != SessionState.Disconnected)
                await _session.LeaveAsync(ct);

            if (SceneManager.GetActiveScene().name != ScenePaths.MAIN_MENU_SCENE)
                await _sceneLoader.LoadSceneAsync(ScenePaths.MAIN_MENU_SCENE, LoadSceneMode.Single, ct);

            await _stateMachine.EnterAsync<MainMenuState>(ct);
        }

        public UniTask ExitAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
