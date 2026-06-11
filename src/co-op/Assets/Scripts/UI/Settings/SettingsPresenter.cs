using Data.UI;
using Infrastructure.Services.Settings;
using Infrastructure.Services.UI;
using UI.Common;

namespace UI.Settings
{
    public sealed class SettingsPresenter : IPresenter
    {
        private readonly SettingsView _view;
        private readonly ISettingsService _settings;
        private readonly IWindowService _windows;

        public SettingsPresenter(SettingsView view, ISettingsService settings, IWindowService windows)
        {
            _view = view;
            _settings = settings;
            _windows = windows;
        }

        public void Initialize()
        {
            _view.MasterVolumeChanged += OnMasterVolume;
            _view.SensitivityChanged += OnSensitivity;
            _view.BackClicked += OnBack;

            _view.SetMasterVolume(_settings.MasterVolume);
            _view.SetSensitivity(_settings.MouseSensitivity);
        }

        public void Dispose()
        {
            _view.MasterVolumeChanged -= OnMasterVolume;
            _view.SensitivityChanged -= OnSensitivity;
            _view.BackClicked -= OnBack;
        }

        private void OnMasterVolume(float v) => _settings.SetMasterVolume(v);
        private void OnSensitivity(float v) => _settings.SetMouseSensitivity(v);
        private void OnBack() => _windows.Close(WindowID.Settings);
    }
}
