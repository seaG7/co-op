using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Infrastructure.Services.UI;

namespace Core.States
{
    public sealed class GameOverState : IState
    {
        private readonly IWindowService _windowService;

        public GameOverState(IWindowService windowService) => _windowService = windowService;

        public UniTask EnterAsync(CancellationToken ct)
        {
            _windowService.Open(WindowID.GameOver);
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken ct)
        {
            _windowService.Close(WindowID.GameOver);
            return UniTask.CompletedTask;
        }
    }
}
