using UnityEngine;

namespace Signals
{
    public readonly struct PlayerDownedSignal
    {
        public readonly int ClientId;
        public readonly bool IsLocal;
        public PlayerDownedSignal(int clientId, bool isLocal) { ClientId = clientId; IsLocal = isLocal; }
    }

    public readonly struct PlayerRevivedSignal
    {
        public readonly int ClientId;
        public readonly bool IsLocal;
        public PlayerRevivedSignal(int clientId, bool isLocal) { ClientId = clientId; IsLocal = isLocal; }
    }

    public readonly struct PlayerDiedSignal
    {
        public readonly int ClientId;
        public readonly bool IsLocal;
        public PlayerDiedSignal(int clientId, bool isLocal) { ClientId = clientId; IsLocal = isLocal; }
    }

    public readonly struct AllPlayersDownedOrDeadSignal { }

    public readonly struct PlayerMeleeSignal
    {
        public readonly Vector3 Position;
        public readonly bool Hit;
        public PlayerMeleeSignal(Vector3 position, bool hit) { Position = position; Hit = hit; }
    }

    public readonly struct MeleePromptSignal
    {
        public readonly bool Show;
        public MeleePromptSignal(bool show) { Show = show; }
    }
}
