using FishNet.Broadcast;

namespace Infrastructure.Services.Lobby
{
    public struct LobbyMember
    {
        public int ClientId;
        public string Nick;
        public bool Ready;
    }

    public struct SetNicknameBroadcast : IBroadcast
    {
        public string Nick;
    }

    public struct SetReadyBroadcast : IBroadcast
    {
        public bool Ready;
    }

    public struct LobbyStateBroadcast : IBroadcast
    {
        public LobbyMember[] Members;
    }

    public struct GameStartingBroadcast : IBroadcast
    {
    }
}
