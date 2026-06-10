using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data.UI;
using Infrastructure.Services.UI;
using UnityEngine;

namespace Core.States
{
    public sealed class MainMenuState : IState
    {
        private readonly IWindowService _windowService;

        public MainMenuState(IWindowService windowService) => _windowService = windowService;

        public UniTask EnterAsync(CancellationToken ct)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _windowService.Open(WindowID.MainMenu);
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken ct)
        {
            _windowService.Close(WindowID.MainMenu);
            _windowService.Close(WindowID.Connect);
            return UniTask.CompletedTask;
        }
    }
}
