using FishNet.Broadcast;

namespace Infrastructure.Services.Lobby
{
    public struct LobbyMember
    {
        public int ClientId;
        public string Nick;
    }

    public struct SetNicknameBroadcast : IBroadcast
    {
        public string Nick;
    }

    public struct LobbyStateBroadcast : IBroadcast
    {
        public LobbyMember[] Members;
        public int LeaderClientId;
    }

    public struct RequestStartBroadcast : IBroadcast
    {
    }

    public struct GameStartingBroadcast : IBroadcast
    {
    }
}
