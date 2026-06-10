using System;
using TMPro;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Room
{
    public class RoomView : WindowView<RoomPresenter>
    {
        [Header("Local player")]
        [SerializeField] private TMP_InputField _nicknameInput;
        [SerializeField] private Toggle _readyToggle;

        [Header("Remote player slot")]
        [Tooltip("Shown while the second player has not joined yet.")]
        [SerializeField] private GameObject _remoteWaitingRoot;
        [Tooltip("Shown when the second player is present (their nick + readiness).")]
        [SerializeField] private GameObject _remoteReadyRoot;
        [SerializeField] private TMP_Text _remoteNameLabel;
        [SerializeField] private TMP_Text _remoteReadyLabel;

        [Header("Controls")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _leaveButton;
        [SerializeField] private TMP_Text _statusLabel;

        public event Action<string> NicknameChanged;
        public event Action<bool> ReadyChanged;
        public event Action StartClicked;
        public event Action LeaveClicked;

        protected override void OnBound()
        {
            if (_startButton != null) _startButton.onClick.AddListener(RaiseStart);
            if (_leaveButton != null) _leaveButton.onClick.AddListener(RaiseLeave);
            if (_nicknameInput != null) _nicknameInput.onEndEdit.AddListener(RaiseNickname);
            if (_readyToggle != null) _readyToggle.onValueChanged.AddListener(RaiseReady);
        }

        protected override void OnUnbinding()
        {
            if (_startButton != null) _startButton.onClick.RemoveListener(RaiseStart);
            if (_leaveButton != null) _leaveButton.onClick.RemoveListener(RaiseLeave);
            if (_nicknameInput != null) _nicknameInput.onEndEdit.RemoveListener(RaiseNickname);
            if (_readyToggle != null) _readyToggle.onValueChanged.RemoveListener(RaiseReady);
        }

        private void RaiseStart() => StartClicked?.Invoke();
        private void RaiseLeave() => LeaveClicked?.Invoke();
        private void RaiseNickname(string v) => NicknameChanged?.Invoke(v);
        private void RaiseReady(bool v) => ReadyChanged?.Invoke(v);

        public void SetLocalNickname(string nick)
        {
            if (_nicknameInput != null && _nicknameInput.text != (nick ?? string.Empty))
                _nicknameInput.text = nick ?? string.Empty;
        }

        public void SetLocalReady(bool ready)
        {
            if (_readyToggle != null) _readyToggle.SetIsOnWithoutNotify(ready);
        }

        public void ShowRemote(bool present)
        {
            if (_remoteWaitingRoot != null) _remoteWaitingRoot.SetActive(!present);
            if (_remoteReadyRoot != null) _remoteReadyRoot.SetActive(present);
        }

        public void SetRemote(string nick, bool ready)
        {
            if (_remoteNameLabel != null) _remoteNameLabel.text = nick ?? string.Empty;
            if (_remoteReadyLabel != null) _remoteReadyLabel.text = ready ? "Готов" : "Не готов";
        }

        public void SetStartVisible(bool visible, bool interactable)
        {
            if (_startButton == null) return;
            _startButton.gameObject.SetActive(visible);
            _startButton.interactable = interactable;
        }

        public void SetStatus(string s)
        {
            if (_statusLabel != null) _statusLabel.text = s ?? string.Empty;
        }
    }
}
