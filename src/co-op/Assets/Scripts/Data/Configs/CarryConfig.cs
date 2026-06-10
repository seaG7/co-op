using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Carry Config", fileName = "CarryConfig")]
    public sealed class CarryConfig : ScriptableObject
    {
        [Header("Reach")]
        [Tooltip("Camera-raycast distance for picking up.")]
        public float MaxReach = 2f;
        [Tooltip("Server-side reach tolerance multiplier for latency forgiveness.")]
        public float ServerReachTolerance = 1.2f;

        [Header("Hold")]
        [Tooltip("Default distance from the camera at which a held item floats, used when an item's HoldDistance is 0.")]
        public float DefaultHoldDistance = 0.7f;
        [Tooltip("Seconds the item eases into the hand on pickup instead of teleporting. 0 = instant snap.")]
        public float PickupBlendDuration = 0.25f;
        [Tooltip("Reach speed (m/s) for the pickup ease-in: pickup duration = distance / this (clamped). Lower = the hand visibly travels longer to far items.")]
        public float PickupReachSpeed = 4f;
        [Tooltip("Max pickup ease-in duration (s) regardless of distance.")]
        public float PickupMaxDuration = 0.7f;
        [Tooltip("(Legacy single-hand kinematic follow — superseded by the physical-follow fields below; kept until the old owner-drive path is fully removed.)")]
        public float HeldFollowSharpness = 14f;

        [Header("Physical follow (server velocity-drive)")]
        [Tooltip("Proportional gain for the held item's linear velocity toward the hand target. Higher = snappier/stiffer.")]
        public float FollowResponsiveness = 18f;
        [Tooltip("Max linear speed (m/s) the held item is driven at for a LIGHT item. Scaled down by SpeedMultiplierForMass (heavy = slower follow).")]
        public float FollowMaxSpeed = 9f;
        [Tooltip("Max angular speed (deg/s) the held item is rotated toward the target orientation.")]
        public float FollowMaxAngularSpeed = 720f;

        [Header("Two-handed")]
        [Tooltip("If the two holders' grip points get farther apart than this (meters), the item strains then auto-releases.")]
        public float TwoHandMaxSeparation = 2.5f;

        [Header("Throw / impact")]
        [Tooltip("Multiplier on the item's tracked carry velocity applied on release.")]
        public float ThrowVelocityScaleV2 = 1.0f;
        [Tooltip("Extra forward impulse (m/s) added along the holder's aim on release.")]
        public float ThrowAimImpulse = 2.5f;
        [Tooltip("Default collision impulse above which a carryable fires ItemImpactSignal (used when the item's own FragileImpulse <= 0).")]
        public float DefaultFragileImpulse = 6f;

        [Header("Snap")]
        [Tooltip("Max angle (degrees) between the player's look direction and a socket for a release to snap onto it. Beyond this, releasing DROPS instead of placing — so you don't snap to a socket you aren't looking at. Generous = forgiving.")]
        [Range(5f, 90f)] public float SnapAimMaxAngle = 30f;

        [Header("Locomotion (weight → speed)")]
        [Tooltip("Items at or below this mass (kg) do NOT slow the player at all — carried freely.")]
        public float FreeCarryMass = 5f;
        [Tooltip("Speed slowdown per kg of mass ABOVE FreeCarryMass. mult = 1 / (1 + excessMass × this).")]
        public float MovementSlowdownPerKg = 0.05f;
        [Tooltip("Floor on the speed multiplier so even very heavy items still allow a slow crawl.")]
        [Range(0.05f, 1f)] public float MinSpeedMultiplier = 0.3f;

        [Header("Throw on release")]
        [Tooltip("Multiplier on the item's carry velocity applied when released — higher = punchier throws.")]
        public float ThrowVelocityScale = 1.2f;
        [Tooltip("Hard cap on release velocity (m/s) — anti-cheat + keeps thrown items sane.")]
        public float MaxThrowSpeed = 12f;

        public float SpeedMultiplierForMass(float mass)
        {
            float excess = Mathf.Max(0f, mass - FreeCarryMass);
            if (excess <= 0f) return 1f;
            return Mathf.Max(MinSpeedMultiplier, 1f / (1f + excess * MovementSlowdownPerKg));
        }

        [Header("Debug")]
        public bool DebugDrawRaycast = false;
        public bool DebugDrawGrab = false;
        public bool DebugOverlay = false;

        [ContextMenu("Reset to defaults")]
        private void ResetToDefaults()
        {
            MaxReach = 2f; ServerReachTolerance = 1.2f;
            DefaultHoldDistance = 0.7f;
            PickupBlendDuration = 0.25f;
            PickupReachSpeed = 4f; PickupMaxDuration = 0.7f;
            HeldFollowSharpness = 14f;
            FollowResponsiveness = 18f;
            FollowMaxSpeed = 9f;
            FollowMaxAngularSpeed = 720f;
            TwoHandMaxSeparation = 2.5f;
            ThrowVelocityScaleV2 = 1.0f;
            ThrowAimImpulse = 2.5f;
            DefaultFragileImpulse = 6f;
            SnapAimMaxAngle = 30f;
            FreeCarryMass = 5f;
            MovementSlowdownPerKg = 0.05f;
            MinSpeedMultiplier = 0.3f;
            ThrowVelocityScale = 1.2f;
            MaxThrowSpeed = 12f;
            DebugDrawRaycast = DebugDrawGrab = DebugOverlay = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
