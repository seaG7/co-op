using FishNet.Connection;
using UnityEngine;

namespace Infrastructure.Services.Spawn
{
    public interface INetworkSpawnService
    {
        GameObject SpawnNetworked(GameObject prefab, Vector3 position, Quaternion rotation, NetworkConnection owner = null);
        void Despawn(GameObject instance);
    }
}
