using UnityEngine;

namespace Gameplay.World.Weapon
{
    [DisallowMultipleComponent]
    public sealed class WeaponModulePart : MonoBehaviour
    {
        [Tooltip("Which cannon module this is (1..N). It can only be attached when it is the next module in order.")]
        [Min(1)] [SerializeField] private int _order = 1;

        public int Order => _order;
    }
}
