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
