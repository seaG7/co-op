using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using Gameplay.Player;

namespace Infrastructure.Services.Spawn
{
    public interface IPlayerSpawnService
    {
        UniTask<PlayerNetwork> SpawnPlayerAsync(NetworkConnection conn, CancellationToken ct = default);
        void DespawnPlayer(NetworkConnection conn);
        PlayerNetwork GetSpawned(int clientId);
    }
}
