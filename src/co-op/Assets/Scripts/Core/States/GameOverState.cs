using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data.UI;
using Infrastructure.Services.Network;
using Infrastructure.Services.Round;
using Infrastructure.Services.UI;
using UnityEngine;

namespace Core.States
{
    public sealed class GameOverState : IState
    {
        private readonly IWindowService _windowService;
        private readonly IRoundService _roundService;
        private readonly ISessionService _session;

        public GameOverState(IWindowService windowService, IRoundService roundService, ISessionService session)
        {
            _windowService = windowService;
            _roundService = roundService;
            _session = session;
        }

        public UniTask EnterAsync(CancellationToken ct)
        {
            // Headless dedicated server has no UI/cursor — just record the outcome and idle here
            // (clients show the GameOver screen). Without this it would crash opening the window.
            if (_session.IsServerOnly)
            {
                Debug.Log($"[GameOverState] (server) round ended: {_roundService.Outcome}.");
                return UniTask.CompletedTask;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log($"[GameOverState] Outcome: {_roundService.Outcome}.");
            _windowService.Open(WindowID.GameOver);
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken ct)
        {
            if (!_session.IsServerOnly) _windowService.Close(WindowID.GameOver);
            return UniTask.CompletedTask;
        }
    }
}
