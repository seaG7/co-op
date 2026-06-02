using Gameplay.Player;

namespace Signals
{
    public readonly struct LocalPlayerSpawnedSignal
    {
        public readonly PlayerNetwork Player;
        public LocalPlayerSpawnedSignal(PlayerNetwork player) => Player = player;
    }

    public readonly struct SpawnFailedSignal
    {
        public readonly int ClientId;
        public readonly string Reason;
        public SpawnFailedSignal(int clientId, string reason) { ClientId = clientId; Reason = reason; }
    }

    public readonly struct GameStartedSignal { }
    public readonly struct GameEndedSignal { }
}
