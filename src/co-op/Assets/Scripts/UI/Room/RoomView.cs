using System;
using TMPro;
using UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Room
{
    public class RoomView : WindowView<RoomPresenter>
    {
        [Header("Host slot (left)")]
        [SerializeField] private TMP_InputField _hostInput;
        [SerializeField] private TMP_Text _hostLabel;

        [Header("Client slot (right)")]
        [SerializeField] private TMP_InputField _clientInput;
        [SerializeField] private TMP_Text _clientLabel;
        [SerializeField] private TMP_Text _clientReadyLabel;
        [Tooltip("Shown in the client slot while the second player has not joined yet.")]
        [SerializeField] private GameObject _clientWaitingRoot;

        [Header("Controls")]
        [SerializeField] private Toggle _readyToggle;
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
            if (_hostInput != null) _hostInput.onEndEdit.AddListener(RaiseNickname);
            if (_clientInput != null) _clientInput.onEndEdit.AddListener(RaiseNickname);
            if (_readyToggle != null) _readyToggle.onValueChanged.AddListener(RaiseReady);
        }

        protected override void OnUnbinding()
        {
            if (_startButton != null) _startButton.onClick.RemoveListener(RaiseStart);
            if (_leaveButton != null) _leaveButton.onClick.RemoveListener(RaiseLeave);
            if (_hostInput != null) _hostInput.onEndEdit.RemoveListener(RaiseNickname);
            if (_clientInput != null) _clientInput.onEndEdit.RemoveListener(RaiseNickname);
            if (_readyToggle != null) _readyToggle.onValueChanged.RemoveListener(RaiseReady);
        }

        private void RaiseStart() => StartClicked?.Invoke();
        private void RaiseLeave() => LeaveClicked?.Invoke();
        private void RaiseNickname(string v) => NicknameChanged?.Invoke(v);
        private void RaiseReady(bool v) => ReadyChanged?.Invoke(v);

        public void SetHostSlot(bool localEditable, string nick)
        {
            SetActive(_hostInput, localEditable);
            SetActive(_hostLabel, !localEditable);
            if (!localEditable && _hostLabel != null) _hostLabel.text = nick ?? string.Empty;
        }

        public void SetClientSlot(bool localEditable, bool present, string nick, bool ready)
        {
            if (localEditable)
            {
                SetActive(_clientInput, true);
                SetActive(_clientLabel, false);
                SetActive(_clientWaitingRoot, false);
                SetReady(_clientReadyLabel, false, ready);
                return;
            }

            SetActive(_clientInput, false);
            SetActive(_clientLabel, present);
            SetActive(_clientWaitingRoot, !present);
            if (present && _clientLabel != null) _clientLabel.text = nick ?? string.Empty;
            SetReady(_clientReadyLabel, present, ready);
        }

        public void SetLocalReady(bool ready)
        {
            if (_readyToggle != null) _readyToggle.SetIsOnWithoutNotify(ready);
        }

        public void ShowReadyToggle(bool show)
        {
            if (_readyToggle != null) _readyToggle.gameObject.SetActive(show);
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

        private static void SetReady(TMP_Text label, bool visible, bool ready)
        {
            if (label == null) return;
            label.gameObject.SetActive(visible);
            if (visible) label.text = ready ? "Готов" : "Не готов";
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null) c.gameObject.SetActive(active);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
