using System;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Pause
{
    public class PauseView : WindowView<PausePresenter>
    {
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _exitButton;

        public event Action ResumeClicked;
        public event Action SettingsClicked;
        public event Action ExitClicked;

        protected override void OnBound()
        {
            if (_resumeButton != null) _resumeButton.onClick.AddListener(RaiseResume);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(RaiseSettings);
            if (_exitButton != null) _exitButton.onClick.AddListener(RaiseExit);
        }

        protected override void OnUnbinding()
        {
            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(RaiseResume);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(RaiseSettings);
            if (_exitButton != null) _exitButton.onClick.RemoveListener(RaiseExit);
        }

        private void RaiseResume() => ResumeClicked?.Invoke();
        private void RaiseSettings() => SettingsClicked?.Invoke();
        private void RaiseExit() => ExitClicked?.Invoke();
    }
}
