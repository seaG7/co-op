namespace UI.Common
{
    public sealed class EmptyPresenter : IPresenter
    {
        public EmptyPresenter(WindowView view) { _ = view; }

        public void Initialize() { }
        public void Dispose() { }
    }
}
