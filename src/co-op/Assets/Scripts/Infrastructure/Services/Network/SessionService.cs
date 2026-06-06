using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Transporting;
using Infrastructure.Providers.Configs;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Network
{
    public sealed class SessionService : ISessionService, IInitializable, IDisposable
    {
        private readonly INetworkService _network;
        private readonly IConfigDataProvider _configs;
        private readonly SignalBus _signalBus;

        private readonly List<int> _clientIds = new();
        private SessionState _state = SessionState.Disconnected;

        public SessionState State => _state;
        public string LastError { get; private set; }

        public int LocalClientId =>
            _network?.NetworkManager?.ClientManager?.Connection != null
                ? _network.NetworkManager.ClientManager.Connection.ClientId
                : -1;

        public IReadOnlyList<int> ConnectedClientIds => _clientIds;

        public event Action<SessionState> StateChanged;
        public event Action<int> ClientJoined;
        public event Action<int> ClientLeft;

        public SessionService(INetworkService network, IConfigDataProvider configs, SignalBus signalBus)
        {
            _network = network;
            _configs = configs;
            _signalBus = signalBus;
        }

        private string LocalhostAddress => _configs?.Network?.LocalhostAddress ?? "127.0.0.1";

        public void Initialize()
        {
            if (_network?.NetworkManager == null)
            {
                Debug.LogError("[SessionService] INetworkService.NetworkManager is null.");
                return;
            }

            _network.NetworkManager.ServerManager.OnRemoteConnectionState += OnRemoteConn;
            _network.ServerStopped += OnServerStopped;
            _network.ClientStopped += OnClientStopped;
            _network.ConnectionFailed += OnConnectionFailed;
        }

        public void Dispose()
        {
            if (_network?.NetworkManager == null) return;
            _network.NetworkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConn;
            _network.ServerStopped -= OnServerStopped;
            _network.ClientStopped -= OnClientStopped;
            _network.ConnectionFailed -= OnConnectionFailed;
        }

        public async UniTask<bool> StartHostAsync(ushort port, CancellationToken ct = default)
        {
            if (_state != SessionState.Disconnected && _state != SessionState.Failed)
            {
                LastError = $"Cannot start host from state {_state}";
                Debug.LogWarning($"[SessionService] {LastError}");
                return false;
            }

            SetState(SessionState.StartingServer);
            if (!await _network.StartServerAsync(port, ct))
            {
                LastError = "Failed to start server";
                SetState(SessionState.Failed);
                return false;
            }

            SetState(SessionState.StartingClient);
            if (!await _network.StartClientAsync(LocalhostAddress, port, ct))
            {
                await _network.StopAsync(CancellationToken.None);
                LastError = "Failed to start host client";
                SetState(SessionState.Failed);
                return false;
            }

            SetState(SessionState.Connected);
            SeedConnectedClientsFromServer();
            _signalBus.Fire(new ServerStartedSignal(port));
            return true;
        }

        private void SeedConnectedClientsFromServer()
        {
            var clients = _network?.NetworkManager?.ServerManager?.Clients;
            if (clients != null)
            {
                foreach (var kv in clients)
                    if (!_clientIds.Contains(kv.Key)) _clientIds.Add(kv.Key);
            }

            var localId = LocalClientId;
            if (localId >= 0 && !_clientIds.Contains(localId)) _clientIds.Add(localId);
        }

        public async UniTask<bool> JoinAsync(string address, ushort port, CancellationToken ct = default)
        {
            if (_state != SessionState.Disconnected && _state != SessionState.Failed)
            {
                LastError = $"Cannot join from state {_state}";
                Debug.LogWarning($"[SessionService] {LastError}");
                return false;
            }

            SetState(SessionState.StartingClient);

            using var reg = ct.Register(() =>
            {
                if (_state == SessionState.StartingClient || _state == SessionState.Connected)
                    SetState(SessionState.Disconnecting);
            });

            var ok = await _network.StartClientAsync(address, port, ct);

            if (ct.IsCancellationRequested)
            {
                SetState(SessionState.Disconnected);
                return false;
            }

            if (!ok)
            {
                if (string.IsNullOrEmpty(LastError)) LastError = "Connection failed";
                SetState(SessionState.Failed);
                return false;
            }

            SetState(SessionState.Connected);
            return true;
        }

        public async UniTask LeaveAsync(CancellationToken ct = default)
        {
            if (_state == SessionState.Disconnected) return;
            SetState(SessionState.Disconnecting);
            await _network.StopAsync(ct);
            _clientIds.Clear();
            SetState(SessionState.Disconnected);
        }

        private void OnRemoteConn(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (conn == null) return;
            var id = conn.ClientId;
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                if (!_clientIds.Contains(id)) _clientIds.Add(id);
                ClientJoined?.Invoke(id);
                _signalBus.Fire(new ClientConnectedSignal(id));
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                _clientIds.Remove(id);
                ClientLeft?.Invoke(id);
                _signalBus.Fire(new ClientDisconnectedSignal(id));
            }
        }

        private void OnServerStopped()
        {
            _clientIds.Clear();
            if (_state == SessionState.Connected || _state == SessionState.StartingServer)
                SetState(SessionState.Disconnected);
            _signalBus.Fire(new ServerStoppedSignal());
        }

        private void OnClientStopped()
        {
            if (_state == SessionState.Connected || _state == SessionState.StartingClient)
            {
                if (_state != SessionState.Disconnecting)
                {
                    LastError = "Connection lost";
                    _signalBus.Fire(new ConnectionLostSignal(LastError));
                }
                SetState(SessionState.Disconnected);
            }
        }

        private void OnConnectionFailed(string reason)
        {
            LastError = reason;
            SetState(SessionState.Failed);
            _signalBus.Fire(new ConnectionFailedSignal(reason));
        }

        private void SetState(SessionState s)
        {
            if (_state == s) return;
            _state = s;
            StateChanged?.Invoke(s);
        }
    }
}
