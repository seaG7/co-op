using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Infrastructure.Providers.Configs;

namespace Core.States
{
    public sealed class BootstrapState : IState
    {
        private readonly IGameStateMachine _stateMachine;
        private readonly IConfigDataProvider _configDataProvider;

        public BootstrapState(IGameStateMachine stateMachine, IConfigDataProvider configDataProvider)
        {
            _stateMachine = stateMachine;
            _configDataProvider = configDataProvider;
        }

        public async UniTask EnterAsync(CancellationToken ct)
        {
            await _configDataProvider.LoadAsync(ct);
            await _stateMachine.EnterAsync<LoadMainMenuState>(ct);
        }

        public UniTask ExitAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
