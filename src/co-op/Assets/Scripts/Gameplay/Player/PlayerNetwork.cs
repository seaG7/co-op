using FishNet.Object;
using Infrastructure.Services.Player;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.Player
{
    public class PlayerNetwork : NetworkBehaviour
    {
        [Inject] private IPlayerService _playerService;
        [Inject] private SignalBus _signalBus;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!base.IsOwner) return;

            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            if (_playerService == null)
            {
                Debug.LogError("[PlayerNetwork] IPlayerService not injected. Check Player.prefab has GameObjectContext + PlayerInstaller.");
                return;
            }

            _playerService.RegisterLocalPlayer(this);
            _signalBus.Fire(new LocalPlayerSpawnedSignal(this));
        }

        public override void OnStopClient()
        {
            if (base.IsOwner && _playerService != null)
                _playerService.UnregisterLocalPlayer(this);
            base.OnStopClient();
        }
    }
}
