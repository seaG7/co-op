namespace Infrastructure.Services.Lobby
{
    public interface ILobbyService
    {
        bool IsLeader { get; }
        int LocalClientId { get; }
        LobbyMember[] Members { get; }
        bool CanStart { get; }

        void SetLocalNickname(string nick);
        void StartGame();
        void RefreshLobby();
        string GetNickname(int clientId);
    }
}
