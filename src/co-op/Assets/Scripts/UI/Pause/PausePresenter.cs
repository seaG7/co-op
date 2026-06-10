using System;
using System.Threading;
using Core.StateMachine;
using Core.States;
using Cysharp.Threading.Tasks;
using Data.UI;
using Infrastructure.Services.Input;
using Infrastructure.Services.Network;
using Infrastructure.Services.UI;
using UI.Common;
using UnityEngine;

namespace UI.Pause
{
    public sealed class PausePresenter : IPresenter
    {
        private readonly PauseView _view;
        private readonly IWindowService _windows;
        private readonly IInputService _input;
        private readonly ISessionService _session;
        private readonly IGameStateMachine _stateMachine;

        private CursorLockMode _savedLock;
        private bool _savedVisible;
        private bool _inputWasEnabled;
        private bool _busy;

        public PausePresenter(PauseView view,
                              IWindowService windows,
                              IInputService input,
                              ISessionService session,
                              IGameStateMachine stateMachine)
        {
            _view = view;
            _windows = windows;
            _input = input;
            _session = session;
            _stateMachine = stateMachine;
        }

        public void Initialize()
        {
            _view.ResumeClicked += OnResume;
            _view.SettingsClicked += OnSettings;
            _view.ExitClicked += OnExit;

            _savedLock = Cursor.lockState;
            _savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _inputWasEnabled = _input != null && _input.IsEnabled;
            _input?.Disable();
        }

        public void Dispose()
        {
            _view.ResumeClicked -= OnResume;
            _view.SettingsClicked -= OnSettings;
            _view.ExitClicked -= OnExit;

            Cursor.lockState = _savedLock;
            Cursor.visible = _savedVisible;
            if (_inputWasEnabled) _input?.Enable();
        }

        private void OnResume() => _windows.Close(WindowID.Pause);

        private void OnSettings() => _windows.Open(WindowID.Settings);

        private async void OnExit()
        {
            if (_busy) return;
            _busy = true;
            _windows.Close(WindowID.Pause);
            try
            {
                await _session.LeaveAsync(CancellationToken.None);
                await _stateMachine.EnterAsync<LoadMainMenuState>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PausePresenter] Exit to menu failed: {ex}");
                _busy = false;
            }
        }
    }
}
