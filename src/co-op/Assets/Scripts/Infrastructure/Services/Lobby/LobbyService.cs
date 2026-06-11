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
        private int _leaderClientId = -1;
        private string _localNick = "Player";

        public LobbyService(INetworkService network, ISessionService session, SignalBus signalBus)
        {
            _network = network;
            _session = session;
            _signalBus = signalBus;
        }

        public int LocalClientId => _session?.LocalClientId ?? -1;
        public LobbyMember[] Members => _members;
        public bool IsLeader => LocalClientId >= 0 && LocalClientId == _leaderClientId;
        public bool CanStart => IsLeader;

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
            nm.ServerManager.RegisterBroadcast<RequestStartBroadcast>(OnRequestStart, requireAuthentication: false);
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
                nm.ServerManager.UnregisterBroadcast<RequestStartBroadcast>(OnRequestStart);
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

        public void RefreshLobby() => BroadcastLobby();

        public void StartGame()
        {
            if (!IsLeader)
            {
                UnityEngine.Debug.LogWarning($"[LobbyService] StartGame blocked: not leader (local={LocalClientId}, leader={_leaderClientId}).");
                return;
            }
            var nm = _network?.NetworkManager;
            if (nm != null && nm.IsClientStarted)
                nm.ClientManager.Broadcast(new RequestStartBroadcast());
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
            _leaderClientId = -1;
            _signalBus?.Fire(new LobbyChangedSignal());
        }

        private void OnClientJoined(int clientId)
        {
            if (!_serverMembers.ContainsKey(clientId))
                _serverMembers[clientId] = new LobbyMember { ClientId = clientId, Nick = $"Player {clientId}" };
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

        private void OnRequestStart(NetworkConnection conn, RequestStartBroadcast msg, Channel channel)
        {
            if (conn == null) return;
            var nm = _network?.NetworkManager;
            if (nm == null || !nm.IsServerStarted) return;

            int leader = ComputeLeaderId();
            if (conn.ClientId != leader)
            {
                UnityEngine.Debug.LogWarning($"[LobbyService] RequestStart from {conn.ClientId} rejected (leader={leader}).");
                return;
            }

            nm.ServerManager.Broadcast(new GameStartingBroadcast());
            if (_session != null && _session.IsServerOnly)
                _signalBus?.Fire(new LobbyGameStartingSignal());
        }

        private int ComputeLeaderId()
        {
            var nm = _network?.NetworkManager;
            if (nm == null || !nm.IsServerStarted) return -1;
            int leader = -1;
            foreach (var kv in nm.ServerManager.Clients)
            {
                if (kv.Value == null) continue;
                if (leader < 0 || kv.Key < leader) leader = kv.Key;
            }
            return leader;
        }

        private void BroadcastLobby()
        {
            var nm = _network?.NetworkManager;
            if (nm == null || !nm.IsServerStarted) return;
            var clients = nm.ServerManager.Clients;
            var list = new List<LobbyMember>(clients.Count);
            int leader = -1;
            foreach (var kv in clients)
            {
                if (kv.Value == null) continue;
                int id = kv.Key;
                if (leader < 0 || id < leader) leader = id;
                if (!_serverMembers.TryGetValue(id, out var m))
                    m = new LobbyMember { ClientId = id, Nick = $"Player {id}" };
                m.ClientId = id;
                list.Add(m);
            }
            nm.ServerManager.Broadcast(new LobbyStateBroadcast { Members = list.ToArray(), LeaderClientId = leader });
        }

        private void OnLobbyState(LobbyStateBroadcast msg, Channel channel)
        {
            _members = msg.Members ?? Array.Empty<LobbyMember>();
            _leaderClientId = msg.LeaderClientId;
            _signalBus?.Fire(new LobbyChangedSignal());
        }

        private void OnGameStarting(GameStartingBroadcast msg, Channel channel)
        {
            _signalBus?.Fire(new LobbyGameStartingSignal());
        }
    }
}
