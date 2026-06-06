using System;
using System.Threading;
using Core.StateMachine;
using Core.States;
using Cysharp.Threading.Tasks;
using Infrastructure.Services.Network;
using UI.Common;
using UnityEngine;

namespace UI.GameOver
{
    public sealed class GameOverPresenter : IPresenter
    {
        private readonly GameOverView _view;
        private readonly IGameStateMachine _stateMachine;
        private readonly ISessionService _session;

        private bool _leaving;

        public GameOverPresenter(GameOverView view, IGameStateMachine sm, ISessionService session)
        {
            _view = view; _stateMachine = sm; _session = session;
        }

        public void Initialize() => _view.BackToMenuClicked += OnBack;
        public void Dispose() => _view.BackToMenuClicked -= OnBack;

        private async void OnBack()
        {
            if (_leaving) return;
            _leaving = true;
            try
            {
                await _session.LeaveAsync(CancellationToken.None);
                await _stateMachine.EnterAsync<LoadMainMenuState>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameOverPresenter] Back-to-menu failed: {ex}");
                _leaving = false;
            }
        }
    }
}
