using System.Threading;
using Core.StateMachine;
using Core.States;
using Cysharp.Threading.Tasks;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Network;
using Infrastructure.Services.UI;
using UI.Common;
using UnityEngine;

namespace UI.Connect
{
    public sealed class ConnectPresenter : IPresenter
    {
        private readonly ConnectView _view;
        private readonly ISessionService _session;
        private readonly IGameStateMachine _stateMachine;
        private readonly IWindowService _windowService;
        private readonly IConfigDataProvider _configs;

        private readonly ConnectFormModel _model = new();
        private CancellationTokenSource _cts;

        public ConnectPresenter(ConnectView view,
                                ISessionService session,
                                IGameStateMachine stateMachine,
                                IWindowService windowService,
                                IConfigDataProvider configs)
        {
            _view = view; _session = session; _stateMachine = stateMachine;
            _windowService = windowService; _configs = configs;
        }

        public void Initialize()
        {
            _view.ConnectClicked += OnConnect;
            _view.BackClicked += OnBack;
            var net = _configs?.Network;
            _model.Address = net != null ? net.DefaultAddress : "127.0.0.1";
            _model.Port = (net != null ? net.DefaultPort : (ushort)7777).ToString();
            _view.Render(_model);
        }

        public void Dispose()
        {
            _view.ConnectClicked -= OnConnect;
            _view.BackClicked -= OnBack;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async void OnConnect()
        {
            var address = _view.Address?.Trim();
            if (string.IsNullOrEmpty(address))
            {
                _model.Error = "Invalid address"; _view.Render(_model); return;
            }
            if (!ushort.TryParse(_view.Port, out var port))
            {
                _model.Error = "Invalid port"; _view.Render(_model); return;
            }

            _model.Busy = true; _model.Error = null; _view.Render(_model);

            _cts?.Cancel(); _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var localCts = _cts;

            var ok = await _session.JoinAsync(address, port, localCts.Token);

            if (_cts == null || _view == null) return;
            if (localCts.IsCancellationRequested) return;

            _model.Busy = false;
            if (!ok)
            {
                _model.Error = _session.LastError ?? "Connection failed";
                _view.Render(_model);
                return;
            }

            _windowService.Close(WindowID.Connect);
            _stateMachine.EnterAsync<LoadGameState>().Forget();
        }

        private void OnBack()
        {
            _cts?.Cancel();
            _windowService.Close(WindowID.Connect);
        }
    }
}
