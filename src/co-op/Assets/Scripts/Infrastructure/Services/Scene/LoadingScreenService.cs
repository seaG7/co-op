using Data.UI;
using Infrastructure.Services.UI;
using UI.Common;

namespace Infrastructure.Services.Scene
{
    public sealed class LoadingScreenService : ILoadingScreenService
    {
        private readonly IWindowService _windowService;

        public LoadingScreenService(IWindowService windowService) => _windowService = windowService;

        public void Show()
        {
            var view = Resolve();
            if (view == null) return;
            view.gameObject.SetActive(true);
            view.ResetBar();
        }

        public void SetProgress(float p)
        {
            if (LoadingBarView.Instance != null) LoadingBarView.Instance.SetProgress(p);
        }

        public void Hide()
        {
            if (LoadingBarView.Instance != null) LoadingBarView.Instance.gameObject.SetActive(false);
        }

        private LoadingBarView Resolve()
        {
            if (LoadingBarView.Instance == null)
                _windowService.OpenAndGet<LoadingBarView>(WindowID.Loading);
            return LoadingBarView.Instance;
        }
    }
}
