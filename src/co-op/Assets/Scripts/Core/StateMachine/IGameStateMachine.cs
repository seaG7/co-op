using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.StateMachine
{
    public interface IGameStateMachine
    {
        IState CurrentState { get; }
        UniTask EnterAsync<TState>(CancellationToken ct = default) where TState : class, IState;
    }
}
