using System.Collections.Generic;

namespace Gameplay.World.Enemies.AI
{
    public sealed class EnemyStateMachine
    {
        private readonly Dictionary<EnemyStateId, IEnemyState> _states = new();
        private readonly EnemyContext _ctx;
        private IEnemyState _current;

        public EnemyStateId CurrentId => _current != null ? _current.Id : EnemyStateId.Pursue;

        public EnemyStateMachine(EnemyContext ctx, IEnumerable<IEnemyState> states, EnemyStateId initial)
        {
            _ctx = ctx;
            foreach (var s in states) _states[s.Id] = s;
            _current = _states[initial];
            _current.Enter(_ctx);
        }

        public void Tick(float dt)
        {
            if (_current == null) return;
            EnemyStateId next = _current.Tick(_ctx, dt);
            if (next != _current.Id && _states.TryGetValue(next, out var ns))
            {
                _current.Exit(_ctx);
                _current = ns;
                _current.Enter(_ctx);
            }
        }

        public void Force(EnemyStateId id)
        {
            if (!_states.TryGetValue(id, out var ns) || ns == _current) return;
            _current?.Exit(_ctx);
            _current = ns;
            _current.Enter(_ctx);
        }
    }
}
