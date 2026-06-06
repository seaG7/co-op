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

    public readonly struct DownStateProgressSignal
    {
        public readonly int ClientId;
        public readonly bool IsLocal;
        public readonly float SecondsRemaining;
        public readonly float ReviveProgress01;
        public DownStateProgressSignal(int clientId, bool isLocal, float secondsRemaining, float reviveProgress01)
        {
            ClientId = clientId;
            IsLocal = isLocal;
            SecondsRemaining = secondsRemaining;
            ReviveProgress01 = reviveProgress01;
        }
    }

    public readonly struct AllPlayersDownedOrDeadSignal { }
}
