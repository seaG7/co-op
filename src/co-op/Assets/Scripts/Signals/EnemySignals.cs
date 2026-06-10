using UnityEngine;

namespace Signals
{
    public readonly struct EnemySpawnedSignal
    {
        public readonly Vector3 Position;
        public EnemySpawnedSignal(Vector3 position) { Position = position; }
    }

    public readonly struct EnemyPrePounceSignal
    {
        public readonly Vector3 Position;
        public EnemyPrePounceSignal(Vector3 position) { Position = position; }
    }

    public readonly struct EnemyPouncedSignal
    {
        public readonly Vector3 Position;
        public EnemyPouncedSignal(Vector3 position) { Position = position; }
    }

    public readonly struct EnemyLatchedSignal
    {
        public readonly Vector3 Position;
        public readonly bool OnPlayer;
        public EnemyLatchedSignal(Vector3 position, bool onPlayer) { Position = position; OnPlayer = onPlayer; }
    }

    public readonly struct EnemyDamagedSignal
    {
        public readonly Vector3 Position;
        public EnemyDamagedSignal(Vector3 position) { Position = position; }
    }

    public readonly struct EnemyDiedSignal
    {
        public readonly Vector3 Position;
        public EnemyDiedSignal(Vector3 position) { Position = position; }
    }
}
