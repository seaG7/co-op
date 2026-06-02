using Data.Configs;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Gameplay.World.Items
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Carryable : NetworkBehaviour
    {
        [Header("Config (designer-assigned at edit time on the prefab)")]
        [SerializeField] private InteractableItemConfig _config;

        [Header("Wired components")]
        [SerializeField] private Rigidbody _rb;

        public readonly SyncVar<int> HolderClientId = new(-1);
        public readonly SyncVar<bool> HasBeenGrabbedOnce = new(false);
        public readonly SyncVar<bool> IsSnapped = new(false);

        public Rigidbody Body => _rb;
        public InteractableItemConfig Config => _config;

        public float Mass => _config != null ? _config.Mass : (_rb != null ? _rb.mass : 1f);
        public float MaxCarrySpeed => _config != null ? _config.MaxCarrySpeed : 5f;
        public float HoldDistance => _config != null ? _config.HoldDistance : 0f;

        private void Awake()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_config != null && _rb != null)
                _rb.mass = _config.Mass;
        }
    }
}
