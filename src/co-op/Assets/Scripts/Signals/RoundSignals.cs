namespace Signals
{
    public readonly struct WaveStartedSignal
    {
        public readonly int Index;
        public WaveStartedSignal(int index) { Index = index; }
    }

    public readonly struct WaveClearedSignal
    {
        public readonly int Index;
        public WaveClearedSignal(int index) { Index = index; }
    }

    public readonly struct AllWavesClearedSignal { }
}
