using Data.Rounds;

namespace Infrastructure.Services.Round
{
    public interface IRoundService
    {
        RoundOutcome Outcome { get; }
        int CurrentWaveIndex { get; }

        // Asks the server to restart the round (reload the global scene for everyone).
        void RequestRestart();
    }
}
