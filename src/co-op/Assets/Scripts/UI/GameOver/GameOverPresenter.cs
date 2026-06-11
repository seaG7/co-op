using System;
using System.Threading;
using Core.StateMachine;
using Core.States;
using Cysharp.Threading.Tasks;
using Data.Rounds;
using Infrastructure.Services.Network;
using Infrastructure.Services.Round;
using UI.Common;
using UnityEngine;

namespace UI.GameOver
{
    public sealed class GameOverPresenter : IPresenter
    {
        private readonly GameOverView _view;
        private readonly IGameStateMachine _stateMachine;
        private readonly ISessionService _session;
        private readonly IRoundService _round;

        private bool _busy;

        public GameOverPresenter(GameOverView view, IGameStateMachine sm, ISessionService session, IRoundService round)
        {
            _view = view;
            _stateMachine = sm;
            _session = session;
            _round = round;
        }

        public void Initialize()
        {
            _view.BackToMenuClicked += OnBack;
            _view.RestartClicked += OnRestart;
            ApplyOutcome();
        }

        public void Dispose()
        {
            _view.BackToMenuClicked -= OnBack;
            _view.RestartClicked -= OnRestart;
        }

        private void ApplyOutcome()
        {
            var outcome = _round.Outcome;
            _view.SetOutcomePanels(outcome == RoundOutcome.Victory, outcome == RoundOutcome.Defeat);
            switch (outcome)
            {
                case RoundOutcome.Victory: _view.SetOutcome("ПОБЕДА", new Color(0.5f, 1f, 0.6f)); break;
                case RoundOutcome.Defeat: _view.SetOutcome("ПОРАЖЕНИЕ", new Color(1f, 0.4f, 0.4f)); break;
                default: _view.SetOutcome(string.Empty, Color.white); break;
            }
        }

        private async void OnBack()
        {
            if (_busy) return;
            _busy = true;
            try
            {
                await _session.LeaveAsync(CancellationToken.None);
                await _stateMachine.EnterAsync<LoadMainMenuState>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameOverPresenter] Back-to-menu failed: {ex}");
                _busy = false;
            }
        }

        private void OnRestart()
        {
            if (_busy) return;
            _busy = true;
            // Server-driven: the round controller relays a restart to everyone (incl. the dedicated
            // server, which reloads the global scene). We transition via GameRestartingSignal, not here.
            _round.RequestRestart();
        }
    }
}
