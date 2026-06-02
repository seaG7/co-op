using Infrastructure.Services.UI;
using UI.Common;

namespace Infrastructure.Services.Scene
{
    public sealed class LoadingScreenService : ILoadingScreenService
    {
        private readonly IWindowService _windowService;
        private LoadingBarView _view;

        public LoadingScreenService(IWindowService windowService) => _windowService = windowService;

        public void Show()
        {
            if (_view == null) _view = _windowService.OpenAndGet<LoadingBarView>(WindowID.Loading);
            _view?.SetProgress(0f);
        }

        public void SetProgress(float p) => _view?.SetProgress(p);

        public void Hide()
        {
            if (_view == null) return;
            _windowService.Close(WindowID.Loading);
            _view = null;
        }
    }
}
