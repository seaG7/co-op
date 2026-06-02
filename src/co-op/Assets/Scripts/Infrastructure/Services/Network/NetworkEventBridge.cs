using System;
using System.Linq;
using Data.Paths;
using FishNet.Managing.Scened;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Network
{
    public sealed class NetworkEventBridge : IInitializable, IDisposable
    {
        private readonly INetworkService _network;
        private readonly SignalBus _signalBus;

        public NetworkEventBridge(INetworkService network, SignalBus signalBus)
        {
            _network = network;
            _signalBus = signalBus;
        }

        public void Initialize()
        {
            if (_network?.NetworkManager?.SceneManager == null)
            {
                Debug.LogWarning("[NetworkEventBridge] NetworkManager.SceneManager unavailable; bridge disabled.");
                return;
            }

            _network.NetworkManager.SceneManager.OnLoadEnd += OnSceneLoadEnd;
        }

        public void Dispose()
        {
            if (_network?.NetworkManager?.SceneManager != null)
                _network.NetworkManager.SceneManager.OnLoadEnd -= OnSceneLoadEnd;
        }

        private void OnSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            if (args.LoadedScenes != null && args.LoadedScenes.Any(s => s.name == ScenePaths.GAME_SCENE))
                _signalBus.Fire(new GameStartedSignal());
        }
    }
}
