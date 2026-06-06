using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Infrastructure.Services.Round;
using Infrastructure.Services.UI;
using UnityEngine;

namespace Core.States
{
    public sealed class GameOverState : IState
    {
        private readonly IWindowService _windowService;
        private readonly IRoundService _roundService;

        public GameOverState(IWindowService windowService, IRoundService roundService)
        {
            _windowService = windowService;
            _roundService = roundService;
        }

        public UniTask EnterAsync(CancellationToken ct)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log($"[GameOverState] Outcome: {_roundService.Outcome}.");
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
