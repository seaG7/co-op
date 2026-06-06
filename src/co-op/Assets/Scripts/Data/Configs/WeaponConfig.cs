using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Weapon Config", fileName = "WeaponConfig")]
    public sealed class WeaponConfig : ScriptableObject
    {
        [Tooltip("If true, the weapon only fires when two distinct alive players are manning it (co-op).")]
        public bool RequiresBothOperators = true;

        [Tooltip("How close (m) an alive player must be to an operator station to man it.")]
        public float OperatorRange = 2.5f;

        [Tooltip("Seconds between shots while manned, assembled, and a target is open.")]
        public float FireInterval = 1.5f;

        [Tooltip("Damage dealt to the Source per shot.")]
        public float ShotDamage = 10f;

        [Tooltip("Max range (m) of the hitscan shot.")]
        public float MuzzleRange = 100f;
    }
}
