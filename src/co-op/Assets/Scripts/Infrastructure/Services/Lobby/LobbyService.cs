using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Transporting;
using Infrastructure.Services.Network;
using Signals;
using Zenject;

namespace Infrastructure.Services.Lobby
{
    public sealed class LobbyService : ILobbyService, IInitializable, IDisposable
    {
        private readonly INetworkService _network;
        private readonly ISessionService _session;
        private readonly SignalBus _signalBus;

        private readonly Dictionary<int, LobbyMember> _serverMembers = new();
        private LobbyMember[] _members = Array.Empty<LobbyMember>();
        private string _localNick = "Player";

        public LobbyService(INetworkService network, ISessionService session, SignalBus signalBus)
        {
            _network = network;
            _session = session;
            _signalBus = signalBus;
        }

        public bool IsHost => _network != null && _network.IsServer;
        public int LocalClientId => _session?.LocalClientId ?? -1;
        public LobbyMember[] Members => _members;

        public bool AllReady
        {
            get
            {
                if (_members.Length < 2) return false;
                for (int i = 0; i < _members.Length; i++)
                    if (!_members[i].Ready) return false;
                return true;
            }
        }

        public bool CanStart
        {
            get
            {
                if (!IsHost) return false;
                if (_members.Length == 1) return true;
                return AllReady;
            }
        }

        public string GetNickname(int clientId)
        {
            for (int i = 0; i < _members.Length; i++)
                if (_members[i].ClientId == clientId) return _members[i].Nick;
            return $"Player {clientId}";
        }

        public void Initialize()
        {
            var nm = _network?.NetworkManager;
            if (nm == null) return;
            nm.ServerManager.RegisterBroadcast<SetNicknameBroadcast>(OnSetNickname, requireAuthentication: false);
            nm.ServerManager.RegisterBroadcast<SetReadyBroadcast>(OnSetReady, requireAuthentication: false);
            nm.ClientManager.RegisterBroadcast<LobbyStateBroadcast>(OnLobbyState);
            nm.ClientManager.RegisterBroadcast<GameStartingBroadcast>(OnGameStarting);
            _session.ClientJoined += OnClientJoined;
            _session.ClientLeft += OnClientLeft;
            _session.StateChanged += OnSessionState;
        }

        public void Dispose()
        {
            var nm = _network?.NetworkManager;
            if (nm != null)
            {
                nm.ServerManager.UnregisterBroadcast<SetNicknameBroadcast>(OnSetNickname);
                nm.ServerManager.UnregisterBroadcast<SetReadyBroadcast>(OnSetReady);
                nm.ClientManager.UnregisterBroadcast<LobbyStateBroadcast>(OnLobbyState);
                nm.ClientManager.UnregisterBroadcast<GameStartingBroadcast>(OnGameStarting);
            }
            if (_session != null)
            {
                _session.ClientJoined -= OnClientJoined;
                _session.ClientLeft -= OnClientLeft;
                _session.StateChanged -= OnSessionState;
            }
        }

        public void SetLocalNickname(string nick)
        {
            _localNick = string.IsNullOrWhiteSpace(nick) ? "Player" : nick.Trim();
            var nm = _network?.NetworkManager;
            if (nm != null && nm.IsClientStarted)
                nm.ClientManager.Broadcast(new SetNicknameBroadcast { Nick = _localNick });
        }

        public void SetLocalReady(bool ready)
        {
            var nm = _network?.NetworkManager;
            if (nm != null && nm.IsClientStarted)
                nm.ClientManager.Broadcast(new SetReadyBroadcast { Ready = ready });
        }

        public void RefreshLobby() => BroadcastLobby();

        public void StartGame()
        {
            var nm = _network?.NetworkManager;
            bool server = nm != null && nm.IsServerStarted;
            if (server && CanStart)
            {
                nm.ServerManager.Broadcast(new GameStartingBroadcast());
                return;
            }
            UnityEngine.Debug.LogWarning($"[LobbyService] StartGame blocked: serverStarted={server}, isHost={IsHost}, members={_members.Length}, canStart={CanStart}");
        }

        private void OnSessionState(SessionState state)
        {
            if (state == SessionState.Connected)
            {
                BroadcastLobby();
                return;
            }
            if (state != SessionState.Disconnected) return;
            _serverMembers.Clear();
            _members = Array.Empty<LobbyMember>();
            _signalBus?.Fire(new LobbyChangedSignal());
        }

        private void OnClientJoined(int clientId)
        {
            if (!_serverMembers.ContainsKey(clientId))
                _serverMembers[clientId] = new LobbyMember { ClientId = clientId, Nick = $"Player {clientId}", Ready = false };
            BroadcastLobby();
        }

        private void OnClientLeft(int clientId)
        {
            _serverMembers.Remove(clientId);
            BroadcastLobby();
        }

        private void OnSetNickname(NetworkConnection conn, SetNicknameBroadcast msg, Channel channel)
        {
            if (conn == null) return;
            string nick = string.IsNullOrWhiteSpace(msg.Nick) ? $"Player {conn.ClientId}" : msg.Nick.Trim();
            var m = _serverMembers.TryGetValue(conn.ClientId, out var existing)
                ? existing
                : new LobbyMember { ClientId = conn.ClientId };
            m.ClientId = conn.ClientId;
            m.Nick = nick;
            _serverMembers[conn.ClientId] = m;
            BroadcastLobby();
        }

        private void OnSetReady(NetworkConnection conn, SetReadyBroadcast msg, Channel channel)
        {
            if (conn == null) return;
            var m = _serverMembers.TryGetValue(conn.ClientId, out var existing)
                ? existing
                : new LobbyMember { ClientId = conn.ClientId, Nick = $"Player {conn.ClientId}" };
            m.ClientId = conn.ClientId;
            m.Ready = msg.Ready;
            _serverMembers[conn.ClientId] = m;
            BroadcastLobby();
        }

        private void BroadcastLobby()
        {
            var nm = _network?.NetworkManager;
            if (nm == null || !nm.IsServerStarted) return;
            var clients = nm.ServerManager.Clients;
            var list = new List<LobbyMember>(clients.Count);
            foreach (var kv in clients)
            {
                if (kv.Value == null) continue;
                int id = kv.Key;
                if (!_serverMembers.TryGetValue(id, out var m))
                    m = new LobbyMember { ClientId = id, Nick = $"Player {id}", Ready = false };
                m.ClientId = id;
                list.Add(m);
            }
            nm.ServerManager.Broadcast(new LobbyStateBroadcast { Members = list.ToArray() });
        }

        private void OnLobbyState(LobbyStateBroadcast msg, Channel channel)
        {
            _members = msg.Members ?? Array.Empty<LobbyMember>();
            _signalBus?.Fire(new LobbyChangedSignal());
        }

        private void OnGameStarting(GameStartingBroadcast msg, Channel channel)
        {
            _signalBus?.Fire(new LobbyGameStartingSignal());
        }
    }
}
