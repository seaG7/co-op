using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Core.StateMachine
{
    public sealed class GameStateMachine : IGameStateMachine, IDisposable
    {
        private readonly DiContainer _container;

        private Type _transitioningTo;

        public IState CurrentState { get; private set; }

        public Action OnEnterFailed { get; set; }

        public GameStateMachine(DiContainer container) => _container = container;

        public async UniTask EnterAsync<TState>(CancellationToken ct = default)
            where TState : class, IState
        {
            var target = typeof(TState);

            if (CurrentState is TState) return;

            if (_transitioningTo == target) return;

            _transitioningTo = target;
            try
            {
                var previous = CurrentState;
                if (previous != null)
                {
                    try { await previous.ExitAsync(ct); }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GameStateMachine] {previous.GetType().Name}.ExitAsync threw: {ex}");
                    }
                }

                TState next;
                try
                {
                    next = _container.Resolve<TState>();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameStateMachine] Failed to resolve {typeof(TState).Name}: {ex}");
                    return;
                }

                CurrentState = next;

                try
                {
                    await next.EnterAsync(ct);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameStateMachine] {typeof(TState).Name}.EnterAsync threw: {ex}");
                    OnEnterFailed?.Invoke();
                }
            }
            finally
            {

                if (_transitioningTo == target) _transitioningTo = null;
            }
        }

        public void Dispose()
        {
            OnEnterFailed = null;
        }
    }
}
