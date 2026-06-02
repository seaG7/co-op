using System;
using Cysharp.Threading.Tasks;
using Gameplay.World.Weapon;
using Infrastructure.Services.Network;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Spawn
{
    public sealed class WeaponBaseSpawner : IInitializable, IDisposable
    {
        private readonly INetworkService _network;
        private readonly INetworkSpawnService _spawner;
        private readonly SignalBus _signalBus;
        private bool _disposed;

        public WeaponBaseSpawner(INetworkService network, INetworkSpawnService spawner, SignalBus signalBus)
        {
            _network = network;
            _spawner = spawner;
            _signalBus = signalBus;
        }

        public void Initialize() => _signalBus.Subscribe<LevelReadySignal>(OnLevelReady);

        public void Dispose()
        {
            _disposed = true;
            _signalBus.TryUnsubscribe<LevelReadySignal>(OnLevelReady);
        }

        private void OnLevelReady(LevelReadySignal _) => SpawnAsync().Forget();

        private async UniTaskVoid SpawnAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (_disposed) return;
            if (_network == null || !_network.IsServer) return;

            var bases = UnityEngine.Object.FindObjectsByType<WeaponBase>(FindObjectsSortMode.None);
            if (bases == null || bases.Length == 0)
            {
                Debug.LogWarning("[WeaponBaseSpawner] No WeaponBase in scene — no weapon will spawn.");
                return;
            }

            int spawned = 0;
            foreach (var b in bases)
            {
                if (b == null) continue;
                if (b.WeaponPrefab == null)
                {
                    Debug.LogWarning($"[WeaponBaseSpawner] WeaponBase '{b.name}' has no WeaponPrefab assigned; skipping.", b);
                    continue;
                }
                var go = _spawner.SpawnNetworked(b.WeaponPrefab, b.SpawnWorldPos, b.SpawnWorldRot, owner: null);
                if (go == null)
                {
                    Debug.LogWarning($"[WeaponBaseSpawner] SpawnNetworked returned null for base '{b.name}'.", b);
                    continue;
                }
                spawned++;
            }
            Debug.Log($"[WeaponBaseSpawner] Spawned {spawned} weapon(s) from {bases.Length} WeaponBase(s).");
        }
    }
}
