namespace Signals
{
    public readonly struct ServerStartedSignal
    {
        public readonly ushort Port;
        public ServerStartedSignal(ushort port) => Port = port;
    }

    public readonly struct ServerStoppedSignal { }

    public readonly struct ClientConnectedSignal
    {
        public readonly int ClientId;
        public ClientConnectedSignal(int clientId) => ClientId = clientId;
    }

    public readonly struct ClientDisconnectedSignal
    {
        public readonly int ClientId;
        public ClientDisconnectedSignal(int clientId) => ClientId = clientId;
    }

    public readonly struct ConnectionFailedSignal
    {
        public readonly string Reason;
        public ConnectionFailedSignal(string reason) => Reason = reason;
    }

    public readonly struct ConnectionLostSignal
    {
        public readonly string Reason;
        public ConnectionLostSignal(string reason) => Reason = reason;
    }
}
