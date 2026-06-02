namespace Signals
{
    public readonly struct InteractPromptSignal
    {
        public readonly bool Show;
        public InteractPromptSignal(bool show) => Show = show;
    }
}
