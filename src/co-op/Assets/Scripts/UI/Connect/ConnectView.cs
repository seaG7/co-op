using System;
using TMPro;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Connect
{
    public class ConnectView : WindowView<ConnectPresenter>
    {
        [SerializeField] private TMP_InputField _addressInput;
        [SerializeField] private TMP_InputField _portInput;
        [SerializeField] private Button _connectButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _errorLabel;
        [SerializeField] private GameObject _busyIndicator;

        public event Action ConnectClicked;
        public event Action BackClicked;

        protected override void OnBound()
        {
            if (_connectButton != null) _connectButton.onClick.AddListener(() => ConnectClicked?.Invoke());
            if (_backButton != null) _backButton.onClick.AddListener(() => BackClicked?.Invoke());
        }

        protected override void OnUnbinding()
        {
            if (_connectButton != null) _connectButton.onClick.RemoveAllListeners();
            if (_backButton != null) _backButton.onClick.RemoveAllListeners();
        }

        public void Render(ConnectFormModel m)
        {
            if (_addressInput != null && _addressInput.text != (m.Address ?? string.Empty)) _addressInput.text = m.Address ?? string.Empty;
            if (_portInput != null && _portInput.text != (m.Port ?? string.Empty)) _portInput.text = m.Port ?? string.Empty;

            if (_errorLabel != null)
            {
                _errorLabel.text = m.Error ?? string.Empty;
                _errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(m.Error));
            }
            if (_busyIndicator != null) _busyIndicator.SetActive(m.Busy);
            if (_connectButton != null) _connectButton.interactable = !m.Busy;
            if (_backButton != null) _backButton.interactable = !m.Busy;
        }

        public string Address => _addressInput != null ? _addressInput.text : string.Empty;
        public string Port => _portInput != null ? _portInput.text : string.Empty;
    }
}
