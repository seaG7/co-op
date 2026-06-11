using Data.Players;
using Data.Rounds;

namespace Signals
{
    public readonly struct LocalPlayerSpawnedSignal
    {
        public readonly ILocalPlayer Player;
        public LocalPlayerSpawnedSignal(ILocalPlayer player) => Player = player;
    }

    public readonly struct SpawnFailedSignal
    {
        public readonly int ClientId;
        public readonly string Reason;
        public SpawnFailedSignal(int clientId, string reason) { ClientId = clientId; Reason = reason; }
    }

    public readonly struct GameStartedSignal { }

    public readonly struct GameEndedSignal
    {
        public readonly RoundOutcome Outcome;
        public GameEndedSignal(RoundOutcome outcome) => Outcome = outcome;
    }

    // Server-driven restart: relayed to everyone (incl. the dedicated server) so all peers re-enter
    // LoadGameState and the server reloads the global scene.
    public readonly struct GameRestartingSignal { }
}
