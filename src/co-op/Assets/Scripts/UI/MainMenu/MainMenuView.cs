using System;
using TMPro;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.MainMenu
{
    public class MainMenuView : WindowView<MainMenuPresenter>
    {
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _connectButton;
        [SerializeField] private Button _quitButton;
        [Tooltip("Optional status line for create/join feedback ('Поиск комнаты…', 'Комната не найдена').")]
        [SerializeField] private TMP_Text _statusLabel;

        public event Action HostClicked;
        public event Action ConnectClicked;
        public event Action QuitClicked;

        protected override void OnBound()
        {
            if (_hostButton != null) _hostButton.onClick.AddListener(() => HostClicked?.Invoke());
            if (_connectButton != null) _connectButton.onClick.AddListener(() => ConnectClicked?.Invoke());
            if (_quitButton != null) _quitButton.onClick.AddListener(() => QuitClicked?.Invoke());
        }

        protected override void OnUnbinding()
        {
            if (_hostButton != null) _hostButton.onClick.RemoveAllListeners();
            if (_connectButton != null) _connectButton.onClick.RemoveAllListeners();
            if (_quitButton != null) _quitButton.onClick.RemoveAllListeners();
        }

        public void SetInteractable(bool on)
        {
            if (_hostButton != null) _hostButton.interactable = on;
            if (_connectButton != null) _connectButton.interactable = on;
            if (_quitButton != null) _quitButton.interactable = on;
        }

        public void SetStatus(string s)
        {
            if (_statusLabel != null) _statusLabel.text = s ?? string.Empty;
        }
    }
}
