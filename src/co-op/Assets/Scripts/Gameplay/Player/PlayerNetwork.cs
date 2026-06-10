using Data.Players;
using Gameplay.Net;
using Infrastructure.Services.Player;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.Player
{
    public class PlayerNetwork : InjectableNetworkBehaviour, ILocalPlayer
    {
        [Inject] private IPlayerService _playerService;
        [Inject] private SignalBus _signalBus;

        public Transform Transform => transform;
        public GameObject GameObject => gameObject;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!base.IsOwner) return;

            if (_playerService == null)
            {
                Debug.LogError("[PlayerNetwork] IPlayerService not injected after spawn — runtime injection failed.");
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
