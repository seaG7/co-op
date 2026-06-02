using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Transporting;
using Gameplay.Player;
using Gameplay.World.Spawn;
using Infrastructure.Services.Network;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Spawn
{
    public sealed class PlayerSpawnService : IPlayerSpawnService, IInitializable, IDisposable
    {
        private readonly INetworkService _network;
        private readonly INetworkSpawnService _spawner;
        private readonly GameObject _playerPrefab;
        private readonly SignalBus _signalBus;

        private readonly Dictionary<int, PlayerNetwork> _spawnedByClientId = new();
        private readonly CancellationTokenSource _serviceCts = new();
        private readonly HashSet<int> _claimedFixedIndices = new();

        private PlayerSpawnArea _area;
        private bool _levelReady;

        public PlayerSpawnService(
            INetworkService network,
            INetworkSpawnService spawner,
            [InjectOptional] GameObject playerPrefab,
            SignalBus signalBus)
        {
            _network = network;
            _spawner = spawner;
            _playerPrefab = playerPrefab;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            if (_network?.NetworkManager == null)
            {
                Debug.LogError("[PlayerSpawnService] NetworkManager unavailable.");
                return;
            }

            _network.NetworkManager.ServerManager.OnRemoteConnectionState += OnRemoteConn;
            _signalBus.Subscribe<LevelReadySignal>(OnLevelReady);
        }

        public void Dispose()
        {
            _serviceCts.Cancel();
            _serviceCts.Dispose();

            if (_network?.NetworkManager != null)
                _network.NetworkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConn;

            _signalBus.TryUnsubscribe<LevelReadySignal>(OnLevelReady);

            if (_network?.IsServer == true)
            {
                foreach (var kv in _spawnedByClientId)
                    if (kv.Value != null)
                        _spawner.Despawn(kv.Value.gameObject);
            }
            _spawnedByClientId.Clear();
        }

        public async UniTask<PlayerNetwork> SpawnPlayerAsync(NetworkConnection conn, CancellationToken ct = default)
        {
            if (_network == null || !_network.IsServer)
            {
                Debug.LogWarning("[PlayerSpawnService] SpawnPlayerAsync called on non-server. Ignored.");
                return null;
            }

            if (conn != null && _spawnedByClientId.TryGetValue(conn.ClientId, out var existing) && existing != null)
                return existing;

            if (_playerPrefab == null)
            {
                var reason = "Player prefab is null in GameSceneInstaller";
                _signalBus.Fire(new SpawnFailedSignal(conn?.ClientId ?? -1, reason));
                Debug.LogError($"[PlayerSpawnService] {reason}.");
                return null;
            }

            ResolveSpawnTransform(out var pos, out var rot);

            var go = _spawner.SpawnNetworked(_playerPrefab, pos, rot, conn);
            if (go == null)
            {
                _signalBus.Fire(new SpawnFailedSignal(conn?.ClientId ?? -1, "NetworkSpawnService returned null"));
                return null;
            }

            var pn = go.GetComponent<PlayerNetwork>();
            if (pn == null)
            {
                _signalBus.Fire(new SpawnFailedSignal(conn?.ClientId ?? -1, "Player prefab missing PlayerNetwork"));
                Debug.LogError($"[PlayerSpawnService] Prefab {_playerPrefab.name} has no PlayerNetwork component.");
                UnityEngine.Object.Destroy(go);
                return null;
            }

            if (conn != null)
                _spawnedByClientId[conn.ClientId] = pn;

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            return pn;
        }

        public void DespawnPlayer(NetworkConnection conn)
        {
            if (_network == null || !_network.IsServer) return;
            if (conn == null) return;
            if (!_spawnedByClientId.Remove(conn.ClientId, out var player)) return;
            if (player != null)
                _spawner.Despawn(player.gameObject);
        }

        public PlayerNetwork GetSpawned(int clientId) =>
            _spawnedByClientId.TryGetValue(clientId, out var p) ? p : null;

        private void ResolveSpawnTransform(out Vector3 pos, out Quaternion rot)
        {
            if (_area == null)
            {
                rot = Quaternion.identity;
                pos = Vector3.zero + Vector3.up * GetCapsuleSpawnLift();
                return;
            }

            var avoid = CollectAlreadySpawnedPositions();
            if (!_area.TrySampleSpawn(avoid, _claimedFixedIndices, out var groundPos, out rot))
            {
                Debug.LogWarning(
                    "[PlayerSpawnService] PlayerSpawnArea couldn't find a valid spawn point " +
                    "(check the Scene-view heatmap — if it's all red the polygon is fully blocked). " +
                    "Falling back to the area's own position.", _area);
                groundPos = _area.transform.position;
                rot = _area.SpawnRotation;
            }
            pos = groundPos + Vector3.up * GetCapsuleSpawnLift();
        }

        private List<Vector3> CollectAlreadySpawnedPositions()
        {
            var list = new List<Vector3>(_spawnedByClientId.Count);
            foreach (var pn in _spawnedByClientId.Values)
                if (pn != null) list.Add(pn.transform.position);
            return list;
        }

        private float GetCapsuleSpawnLift()
        {
            const float clearance = 0.3f;
            var cc = _playerPrefab != null ? _playerPrefab.GetComponent<CharacterController>() : null;
            if (cc == null) return 1f + clearance;
            return (cc.height * 0.5f - cc.center.y) + cc.skinWidth + clearance;
        }

        private void OnLevelReady(LevelReadySignal _) => OnLevelReadyImpl();

        private void OnLevelReadyImpl()
        {
            _levelReady = true;
            if (!_network.IsServer) return;

            _area = UnityEngine.Object.FindFirstObjectByType<PlayerSpawnArea>();
            if (_area == null)
                Debug.LogWarning("[PlayerSpawnService] No PlayerSpawnArea in scene — falling back to world origin.");

            _claimedFixedIndices.Clear();

            var clients = _network.NetworkManager.ServerManager.Clients;
            if (clients == null) return;
            foreach (var kv in clients)
            {
                var conn = kv.Value;
                if (conn == null) continue;
                if (_spawnedByClientId.ContainsKey(conn.ClientId)) continue;
                SpawnPlayerAsync(conn, _serviceCts.Token).Forget();
            }
        }

        private void OnRemoteConn(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (_network == null || !_network.IsServer) return;

            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                if (_levelReady) SpawnPlayerAsync(conn, _serviceCts.Token).Forget();
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
                DespawnPlayer(conn);
        }
    }
}
