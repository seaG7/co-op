namespace Signals
{
    public enum InteractPromptKind
    {
        PickUp,
        Drop,
        PlaceOnSocket,
    }

    public readonly struct InteractPromptSignal
    {
        public readonly bool Show;
        public readonly InteractPromptKind Kind;

        public InteractPromptSignal(bool show, InteractPromptKind kind = InteractPromptKind.PickUp)
        {
            Show = show;
            Kind = kind;
        }
    }
}
