using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Infrastructure.Services.Network
{
    public enum SessionState
    {
        Disconnected,
        StartingServer,
        StartingClient,
        Connected,
        Disconnecting,
        Failed
    }

    public interface ISessionService
    {
        SessionState State { get; }
        bool IsServerOnly { get; }
        string LastError { get; }
        int LocalClientId { get; }
        IReadOnlyList<int> ConnectedClientIds { get; }

        event Action<SessionState> StateChanged;
        event Action<int> ClientJoined;
        event Action<int> ClientLeft;

        UniTask<bool> StartServerOnlyAsync(ushort port, CancellationToken ct = default);
        UniTask<bool> StartHostAsync(ushort port, CancellationToken ct = default);
        UniTask<bool> JoinAsync(string address, ushort port, CancellationToken ct = default);
        UniTask LeaveAsync(CancellationToken ct = default);
    }
}
