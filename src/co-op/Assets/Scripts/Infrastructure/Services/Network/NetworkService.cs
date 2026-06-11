using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using Infrastructure.Providers.Configs;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Infrastructure.Services.Network
{
    public sealed class NetworkService : INetworkService, IInitializable, IDisposable
    {
        private readonly NetworkManager _nm;
        private readonly IConfigDataProvider _configs;

        public bool IsServer => _nm != null && _nm.IsServerStarted;
        public bool IsClient => _nm != null && _nm.IsClientStarted;
        public bool IsHost => IsServer && IsClient;
        public NetworkManager NetworkManager => _nm;

        public event Action ServerStarted;
        public event Action ServerStopped;
        public event Action ClientStarted;
        public event Action ClientStopped;
        public event Action<string> ConnectionFailed;

        public NetworkService([InjectOptional] NetworkManager networkManager, IConfigDataProvider configs)
        {
            _nm = networkManager;
            _configs = configs;
        }

        private float ConnectTimeoutSec => _configs?.Network?.ConnectTimeoutSec ?? 10f;

        public void Initialize()
        {
            if (_nm == null)
            {
                Debug.LogError("[NetworkService] NetworkManager is null. Check ProjectInstaller — NetworkManager prefab field must be assigned.");
                return;
            }

            if (_nm.transform.parent != null)
            {
                _nm.transform.SetParent(null);
                UnityEngine.Object.DontDestroyOnLoad(_nm.gameObject);
                Debug.Log("[NetworkService] Moved NetworkManager to scene root (was parented — U6 DontDestroyOnLoad/TimeManager fix).");
            }

            _nm.ServerManager.OnServerConnectionState += OnServerStateChanged;
            _nm.ClientManager.OnClientConnectionState += OnClientStateChanged;
        }

        public void Dispose()
        {
            if (_nm == null) return;
            _nm.ServerManager.OnServerConnectionState -= OnServerStateChanged;
            _nm.ClientManager.OnClientConnectionState -= OnClientStateChanged;
        }

        public async UniTask<bool> StartServerAsync(ushort port, CancellationToken ct = default)
        {
            if (_nm == null) { RaiseFailed("NetworkManager not available"); return false; }
            if (_nm.IsServerStarted) return true;

            var transportName = GetTransportName();
            if (transportName == null)
            {
                RaiseFailed("Transport is not assigned on NetworkManager. Add a Tugboat (or other) Transport component to the NetworkManager prefab.");
                return false;
            }

            var tcs = new UniTaskCompletionSource<bool>();
            void Handler(ServerConnectionStateArgs args)
            {
                if (args.ConnectionState == LocalConnectionState.Started) tcs.TrySetResult(true);
                else if (args.ConnectionState == LocalConnectionState.Stopped) tcs.TrySetResult(false);
            }
            _nm.ServerManager.OnServerConnectionState += Handler;
            try
            {
                Debug.Log($"[NetworkService] StartConnection on {transportName}, port {port} (NM parent: {(_nm.transform.parent != null ? _nm.transform.parent.name : "<root>")}).");
                _nm.ServerManager.StartConnection(port);
                Debug.Log("[NetworkService] StartConnection returned; awaiting server-started event…");
                var timeout = TimeSpan.FromSeconds(ConnectTimeoutSec);
                var (isTimeout, ok) = await tcs.Task
                    .AttachExternalCancellation(ct)
                    .TimeoutWithoutException(timeout);

                if (ct.IsCancellationRequested) { _nm.ServerManager.StopConnection(true); return false; }
                if (isTimeout) { _nm.ServerManager.StopConnection(true); RaiseFailed($"Server start timeout on port {port} ({transportName})."); return false; }
                if (!ok)
                {
                    RaiseFailed($"Server failed to bind on port {port} ({transportName}). " +
                                "Most likely the port is already in use (another Unity Editor or process running). " +
                                "See FishNet error in console for the exact LiteNetLib reason; if you don't see one, " +
                                "set NetworkManager.Logging.LoggingType = Common in the prefab inspector.");
                    return false;
                }
                return true;
            }
            finally
            {
                _nm.ServerManager.OnServerConnectionState -= Handler;
            }
        }

        public async UniTask<bool> StartClientAsync(string address, ushort port, CancellationToken ct = default)
        {
            if (_nm == null) { RaiseFailed("NetworkManager not available"); return false; }
            if (_nm.IsClientStarted) return true;

            var transportName = GetTransportName();
            if (transportName == null)
            {
                RaiseFailed("Transport is not assigned on NetworkManager. Add a Tugboat (or other) Transport component to the NetworkManager prefab.");
                return false;
            }

            var tcs = new UniTaskCompletionSource<bool>();
            void Handler(ClientConnectionStateArgs args)
            {
                if (args.ConnectionState == LocalConnectionState.Started) tcs.TrySetResult(true);
                else if (args.ConnectionState == LocalConnectionState.Stopped) tcs.TrySetResult(false);
            }
            _nm.ClientManager.OnClientConnectionState += Handler;
            try
            {
                _nm.ClientManager.StartConnection(address, port);
                var timeout = TimeSpan.FromSeconds(ConnectTimeoutSec);
                var (isTimeout, ok) = await tcs.Task
                    .AttachExternalCancellation(ct)
                    .TimeoutWithoutException(timeout);

                if (ct.IsCancellationRequested) { _nm.ClientManager.StopConnection(); return false; }
                if (isTimeout) { _nm.ClientManager.StopConnection(); RaiseFailed($"Connection timeout to {address}:{port} ({transportName})."); return false; }
                if (!ok) { RaiseFailed($"Connection refused or server unreachable at {address}:{port} ({transportName})."); return false; }
                return true;
            }
            finally
            {
                _nm.ClientManager.OnClientConnectionState -= Handler;
            }
        }

        private string GetTransportName()
        {
            var tm = _nm?.TransportManager;
            var t = tm?.Transport;
            return t == null ? null : t.GetType().Name;
        }

        public async UniTask StopAsync(CancellationToken ct = default)
        {
            if (_nm == null) return;
            if (_nm.IsClientStarted) _nm.ClientManager.StopConnection();
            if (_nm.IsServerStarted) _nm.ServerManager.StopConnection(true);
            await UniTask.WaitWhile(() => _nm.IsServerStarted || _nm.IsClientStarted, cancellationToken: ct);
        }

        public async UniTask LoadGlobalSceneAsync(string sceneName, CancellationToken ct = default)
        {
            if (_nm == null) throw new InvalidOperationException("NetworkManager not available");

            var tcs = new UniTaskCompletionSource();
            void Handler(SceneLoadEndEventArgs args)
            {
                if (args.LoadedScenes != null && args.LoadedScenes.Any(s => s.name == sceneName))
                    tcs.TrySetResult();
            }
            _nm.SceneManager.OnLoadEnd += Handler;
            try
            {
                var data = new SceneLoadData(sceneName) { ReplaceScenes = ReplaceOption.All };
                _nm.SceneManager.LoadGlobalScenes(data);
                await tcs.Task.AttachExternalCancellation(ct);
            }
            finally { _nm.SceneManager.OnLoadEnd -= Handler; }
        }

        public async UniTask WaitForSceneLoadedAsync(string sceneName, CancellationToken ct = default)
        {
            if (IsSceneLoaded(sceneName)) return;
            if (_nm == null)
            {
                Debug.LogError("[NetworkService] NetworkManager not available; cannot wait for scene.");
                return;
            }

            var tcs = new UniTaskCompletionSource();
            void Handler(SceneLoadEndEventArgs args)
            {
                if (args.LoadedScenes != null && args.LoadedScenes.Any(s => s.name == sceneName))
                    tcs.TrySetResult();
            }
            _nm.SceneManager.OnLoadEnd += Handler;
            try
            {
                if (IsSceneLoaded(sceneName)) { tcs.TrySetResult(); }
                await tcs.Task.AttachExternalCancellation(ct);
            }
            finally { _nm.SceneManager.OnLoadEnd -= Handler; }
        }

        private static bool IsSceneLoaded(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name == sceneName) return true;
            }
            return false;
        }

        private void OnServerStateChanged(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started) ServerStarted?.Invoke();
            else if (args.ConnectionState == LocalConnectionState.Stopped) ServerStopped?.Invoke();
        }

        private void OnClientStateChanged(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started) ClientStarted?.Invoke();
            else if (args.ConnectionState == LocalConnectionState.Stopped) ClientStopped?.Invoke();
        }

        private void RaiseFailed(string reason)
        {
            Debug.LogWarning($"[NetworkService] {reason}");
            ConnectionFailed?.Invoke(reason);
        }
    }
}
