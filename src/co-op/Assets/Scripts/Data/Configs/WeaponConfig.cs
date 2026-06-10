using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Weapon Config", fileName = "WeaponConfig")]
    public sealed class WeaponConfig : ScriptableObject
    {
        [Header("Fire")]
        [Tooltip("Damage dealt to the Source per shot (the Source is hit-count based, but this is passed through).")]
        public float ShotDamage = 10f;

        [Tooltip("Max range (m) of the hitscan shot.")]
        public float MuzzleRange = 200f;

        [Tooltip("Seconds between shots.")]
        public float FireCooldownSec = 1.5f;

        [Header("Charge")]
        [Tooltip("Mob corpses that must be loaded into the cannon before it can damage the Source. Below this, shots do NO Source damage; at/above, a charged shot destroys it at any time (no timing window).")]
        public int RequiredCorpses = 3;

        [Header("Mounting")]
        [Tooltip("How close (m) a player must be to mount the weapon.")]
        public float MountRange = 3f;

        [Header("Aim heaviness")]
        [Tooltip("Look input → desired turret yaw (deg per input unit).")]
        public float AimYawSensitivity = 0.15f;

        [Tooltip("Look input → desired turret pitch (deg per input unit).")]
        public float AimPitchSensitivity = 0.12f;

        [Tooltip("How fast the turret catches up to the desired aim. LOWER = heavier/laggier (the weight feel).")]
        public float AimResponsiveness = 6f;

        [Tooltip("Max turret yaw from the weapon's forward (± degrees).")]
        public float YawLimit = 70f;

        [Tooltip("Min turret pitch (Euler X; negative aims up).")]
        public float PitchMin = -45f;

        [Tooltip("Max turret pitch (Euler X; positive aims down).")]
        public float PitchMax = 20f;
    }
}
