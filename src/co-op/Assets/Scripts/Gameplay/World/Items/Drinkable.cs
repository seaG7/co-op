using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Gameplay.World.Items
{
    // A bottle you DRINK (not carry). Claimed by one player for the duration of the drink
    // (DrinkerClientId), pinned to that player's drink anchor client-side, then tossed back
    // into the world on completion. Free again afterwards — pick up and drink anew.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Drinkable : NetworkBehaviour
    {
        [SerializeField] private Rigidbody _rb;
        [SerializeField] private FishNet.Component.Transforming.NetworkTransform _networkTransform;
        [Tooltip("Where the hand grips the bottle (child transform). Defaults to the bottle root.")]
        [SerializeField] private Transform _grip;

        public readonly SyncVar<int> DrinkerClientId = new(-1);

        private static readonly List<Drinkable> _all = new();
        public static IReadOnlyList<Drinkable> All => _all;

        private Collider[] _colliders;

        public Transform Grip => _grip != null ? _grip : transform;
        public bool IsClaimed => DrinkerClientId.Value != -1;

        private void Awake()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_networkTransform == null) _networkTransform = GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
            DrinkerClientId.OnChange += OnDrinkerChanged;
            ApplyState();
        }

        public override void OnStopNetwork()
        {
            DrinkerClientId.OnChange -= OnDrinkerChanged;
            _all.Remove(this);
            base.OnStopNetwork();
        }

        private void OnDrinkerChanged(int prev, int next, bool asServer) => ApplyState();

        private void ApplyState()
        {
            bool claimed = DrinkerClientId.Value != -1;
            if (_networkTransform != null) _networkTransform.enabled = !claimed;
            if (_colliders != null)
                for (int i = 0; i < _colliders.Length; i++)
                    if (_colliders[i] != null) _colliders[i].enabled = !claimed;
            if (_rb != null)
            {
                _rb.detectCollisions = !claimed;
                if (claimed)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                    _rb.isKinematic = true;
                }
                else
                {
                    _rb.isKinematic = false;
                }
            }
        }

        public void ServerToss(Vector3 velocity)
        {
            if (!IsServerInitialized) return;
            DrinkerClientId.Value = -1;
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.detectCollisions = true;
                _rb.linearVelocity = velocity;
            }
        }
    }
}
