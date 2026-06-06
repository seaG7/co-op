using Data.Rounds;

namespace Infrastructure.Services.Round
{
    public interface IRoundService
    {
        RoundOutcome Outcome { get; }
        int CurrentWaveIndex { get; }
    }
}
