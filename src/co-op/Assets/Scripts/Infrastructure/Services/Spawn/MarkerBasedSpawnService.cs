using System;
using Cysharp.Threading.Tasks;
using Gameplay.World.Spawn;
using Infrastructure.Services.Network;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Spawn
{
    public sealed class MarkerBasedSpawnService : IMarkerBasedSpawnService, IInitializable, IDisposable
    {
        private readonly INetworkService _network;
        private readonly INetworkSpawnService _spawner;
        private readonly SignalBus _signalBus;

        private bool _disposed;

        public MarkerBasedSpawnService(INetworkService network, INetworkSpawnService spawner, SignalBus signalBus)
        {
            _network = network;
            _spawner = spawner;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            RunOnNextFrameAsync().Forget();
        }

        public void Dispose() => _disposed = true;

        private async UniTaskVoid RunOnNextFrameAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (_disposed) return;

            if (_network == null || !_network.IsServer)
            {
                _signalBus.Fire(new LevelReadySignal());
                return;
            }

            int passed = 0, skipped = 0, missing = 0;
            var markers = UnityEngine.Object.FindObjectsByType<InteractableSpawnMarker>(FindObjectsSortMode.None);
            foreach (var marker in markers)
            {
                if (marker == null) continue;
                if (marker.Config == null || marker.Config.Prefab == null)
                {
                    Debug.LogWarning($"[MarkerBasedSpawnService] Marker '{marker.name}' has no Config/Prefab; skipping.", marker);
                    missing++;
                    continue;
                }

                if (UnityEngine.Random.value > marker.SpawnChance) { skipped++; continue; }

                var go = _spawner.SpawnNetworked(
                    marker.Config.Prefab,
                    marker.transform.position,
                    marker.transform.rotation,
                    owner: null);
                if (go == null)
                {
                    Debug.LogWarning($"[MarkerBasedSpawnService] SpawnNetworked returned null for marker '{marker.name}'.", marker);
                    continue;
                }

                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
                passed++;
            }

            Debug.Log($"[MarkerBasedSpawnService] Spawn pass complete: {passed} spawned, {skipped} skipped by chance, {missing} markers without config.");

            _signalBus.Fire(new LevelReadySignal());
        }
    }
}
