using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Managing;

namespace Infrastructure.Services.Network
{
    public interface INetworkService
    {
        bool IsServer { get; }
        bool IsClient { get; }
        bool IsHost { get; }

        NetworkManager NetworkManager { get; }

        UniTask<bool> StartServerAsync(ushort port, CancellationToken ct = default);
        UniTask<bool> StartClientAsync(string address, ushort port, CancellationToken ct = default);
        UniTask StopAsync(CancellationToken ct = default);

        UniTask LoadGlobalSceneAsync(string sceneName, CancellationToken ct = default);
        UniTask WaitForSceneLoadedAsync(string sceneName, CancellationToken ct = default);

        event Action ServerStarted;
        event Action ServerStopped;
        event Action ClientStarted;
        event Action ClientStopped;
        event Action<string> ConnectionFailed;
    }
}
