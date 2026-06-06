using System;
using Core.StateMachine;
using Core.States;
using Cysharp.Threading.Tasks;
using Data.Rounds;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Round
{
    public sealed class RoundService : IRoundService, IInitializable, IDisposable
    {
        private readonly SignalBus _signalBus;
        private readonly IGameStateMachine _stateMachine;

        public RoundOutcome Outcome { get; private set; } = RoundOutcome.None;
        public int CurrentWaveIndex { get; private set; } = -1;

        public RoundService(SignalBus signalBus, IGameStateMachine stateMachine)
        {
            _signalBus = signalBus;
            _stateMachine = stateMachine;
        }

        public void Initialize()
        {
            _signalBus.Subscribe<GameStartedSignal>(OnGameStarted);
            _signalBus.Subscribe<WaveStartedSignal>(OnWaveStarted);
            _signalBus.Subscribe<GameEndedSignal>(OnGameEnded);
        }

        public void Dispose()
        {
            _signalBus.TryUnsubscribe<GameStartedSignal>(OnGameStarted);
            _signalBus.TryUnsubscribe<WaveStartedSignal>(OnWaveStarted);
            _signalBus.TryUnsubscribe<GameEndedSignal>(OnGameEnded);
        }

        private void OnGameStarted(GameStartedSignal _)
        {
            Outcome = RoundOutcome.None;
            CurrentWaveIndex = -1;
        }

        private void OnWaveStarted(WaveStartedSignal s) => CurrentWaveIndex = s.Index;

        private void OnGameEnded(GameEndedSignal s)
        {
            if (Outcome != RoundOutcome.None) return;
            Outcome = s.Outcome;
            Debug.Log($"[RoundService] Round ended: {s.Outcome}.");
            _stateMachine.EnterAsync<GameOverState>().Forget();
        }
    }
}
