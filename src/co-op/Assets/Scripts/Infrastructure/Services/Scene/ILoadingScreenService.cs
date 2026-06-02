namespace Infrastructure.Services.Scene
{
    public interface ILoadingScreenService
    {
        void Show();
        void SetProgress(float progress);
        void Hide();
    }
}
