namespace Infrastructure.Services.Lobby
{
    public interface ILobbyService
    {
        bool IsHost { get; }
        int LocalClientId { get; }
        LobbyMember[] Members { get; }
        bool AllReady { get; }
        bool CanStart { get; }

        void SetLocalNickname(string nick);
        void SetLocalReady(bool ready);
        void StartGame();
        void RefreshLobby();
        string GetNickname(int clientId);
    }
}
