using System.Threading;
using Core.StateMachine;
using Cysharp.Threading.Tasks;
using Data;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Network;
using UnityEngine;

namespace Core.States
{
    public sealed class BootstrapState : IState
    {
        private readonly IGameStateMachine _stateMachine;
        private readonly IConfigDataProvider _configDataProvider;
        private readonly ISessionService _session;

        public BootstrapState(IGameStateMachine stateMachine,
                              IConfigDataProvider configDataProvider,
                              ISessionService session)
        {
            _stateMachine = stateMachine;
            _configDataProvider = configDataProvider;
            _session = session;
        }

        public async UniTask EnterAsync(CancellationToken ct)
        {
            await _configDataProvider.LoadAsync(ct);

            if (Platform.IsDedicatedServer)
            {
                var port = _configDataProvider.Network != null ? _configDataProvider.Network.DefaultPort : (ushort)7777;
                Debug.Log($"[BootstrapState] Dedicated server detected; starting server on port {port}.");
                var ok = await _session.StartServerOnlyAsync(port, ct);
                if (!ok)
                {
                    Debug.LogError($"[BootstrapState] Dedicated server failed to start: {_session.LastError}");
                    return;
                }
                await _stateMachine.EnterAsync<LobbyState>(ct);
                return;
            }

            await _stateMachine.EnterAsync<LoadMainMenuState>(ct);
        }

        public UniTask ExitAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
