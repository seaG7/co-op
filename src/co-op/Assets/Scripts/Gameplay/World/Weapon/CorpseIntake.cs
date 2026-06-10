using FishNet.Object;
using Gameplay.World.Items;
using UnityEngine;

namespace Gameplay.World.Weapon
{
    [RequireComponent(typeof(Collider))]
    public sealed class CorpseIntake : MonoBehaviour
    {
        [SerializeField] private Weapon _weapon;

        private void Awake()
        {
            if (_weapon == null) _weapon = GetComponentInParent<Weapon>();
            if (_weapon == null) Debug.LogError($"[{nameof(CorpseIntake)}] No Weapon found in parents; corpses will not load.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || _weapon == null) return;
            var corpse = other.GetComponentInParent<Corpse>();
            if (corpse == null) return;
            var no = corpse.GetComponentInParent<NetworkObject>();
            if (no == null || !no.IsServerInitialized) return;
            _weapon.AddCorpse();
            no.Despawn();
        }
    }
}
