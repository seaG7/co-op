using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data.UI;
using Infrastructure.Services.Scene;
using Infrastructure.Services.UI;
using UnityEngine;

namespace Core.States
{
    public sealed class MainMenuState : IState
    {
        private readonly IWindowService _windowService;
        private readonly ILoadingScreenService _loadingScreen;

        public MainMenuState(IWindowService windowService, ILoadingScreenService loadingScreen)
        {
            _windowService = windowService;
            _loadingScreen = loadingScreen;
        }

        public UniTask EnterAsync(CancellationToken ct)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _windowService.Open(WindowID.MainMenu);
            _loadingScreen.Hide();
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
