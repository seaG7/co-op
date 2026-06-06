using System.Collections.Generic;
using FishNet.Connection;
using Infrastructure.Services.DI;
using Infrastructure.Services.Network;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Spawn
{
    public sealed class NetworkSpawnService : INetworkSpawnService
    {
        private readonly INetworkService _network;
        private readonly DiContainer _sceneContainer;

        private static readonly List<IRuntimeInjectable> _injBuffer = new();

        public NetworkSpawnService(INetworkService network, DiContainer sceneContainer)
        {
            _network = network;
            _sceneContainer = sceneContainer;
        }

        public GameObject SpawnNetworked(GameObject prefab, Vector3 position, Quaternion rotation, NetworkConnection owner = null)
        {
            if (_network == null || !_network.IsServer)
            {
                Debug.LogWarning("[NetworkSpawnService] SpawnNetworked called on non-server. Ignored.");
                return null;
            }
            if (prefab == null)
            {
                Debug.LogError("[NetworkSpawnService] Prefab is null.");
                return null;
            }

            var go = _sceneContainer.InstantiatePrefab(prefab, position, rotation, null);
            if (go == null) return null;

            go.transform.GetComponentsInChildren(true, _injBuffer);
            for (int i = 0; i < _injBuffer.Count; i++)
                _injBuffer[i]?.MarkAlreadyInjected();
            _injBuffer.Clear();

            _network.NetworkManager.ServerManager.Spawn(go, owner);
            return go;
        }

        public void Despawn(GameObject instance)
        {
            if (_network == null || !_network.IsServer || instance == null) return;
            _network.NetworkManager.ServerManager.Despawn(instance);
        }
    }
}
