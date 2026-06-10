using System.Collections.Generic;
using Gameplay.Net;
using Gameplay.Player;
using Gameplay.Player.Vitals;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Portals
{
    [RequireComponent(typeof(Collider))]
    public sealed class Portal : InjectableNetworkBehaviour
    {
        private static readonly List<Portal> _all = new();
        public static IReadOnlyList<Portal> All => _all;

        [Inject] private SignalBus _signalBus;

        private readonly HashSet<int> _inside = new();
        private bool _entered;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
        }

        public override void OnStopNetwork()
        {
            _all.Remove(this);
            base.OnStopNetwork();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServerInitialized || _entered || other == null) return;
            var player = other.GetComponentInParent<PlayerNetwork>();
            if (player == null) return;
            _inside.Add(player.OwnerId);
            TryComplete();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServerInitialized || _entered || other == null) return;
            var player = other.GetComponentInParent<PlayerNetwork>();
            if (player == null) return;
            _inside.Remove(player.OwnerId);
        }

        private void TryComplete()
        {
            var players = PlayerVitals.All;
            int alive = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var v = players[i];
                if (v == null || !v.IsAlive) continue;
                alive++;
                if (!_inside.Contains(v.OwnerId)) return;
            }
            if (alive == 0) return;
            _entered = true;
            _signalBus?.Fire(new PortalEnteredSignal());
        }
    }
}
