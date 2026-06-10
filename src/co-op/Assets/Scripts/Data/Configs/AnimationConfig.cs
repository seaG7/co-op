using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Animation Config", fileName = "AnimationConfig")]
    public sealed class AnimationConfig : ScriptableObject
    {
        [Header("Animator parameter names")]
        public string SpeedParam = "Speed";
        public string LocalVelXParam = "LocalVelX";
        public string LocalVelZParam = "LocalVelZ";
        public string VerticalVelocityParam = "VerticalVelocity";
        public string IsGroundedParam = "IsGrounded";
        public string IsCarryingParam = "IsCarrying";
        public string IsDownedParam = "IsDowned";
        public string JumpTrigger = "Jump";
        public string LandTrigger = "Land";
        public string PickUpTrigger = "PickUp";
        public string PickUpSpeedParam = "PickUpSpeed";
        public string GettingUpTrigger = "GettingUp";

        [Header("Locomotion")]
        [Tooltip("Horizontal speed (m/s) mapped to Speed = 1 in the blend tree. Match MovementConfig.MoveSpeed.")]
        public float MaxSpeedForNormalization = 5.5f;
        [Tooltip("Damp time (s) for Speed / planar params — smooths the blend. Lower = snappier idle<->walk.")]
        public float LocomotionDampTime = 0.07f;

        [Header("Airborne")]
        [Tooltip("Upward vertical velocity (m/s) at the moment of leaving ground that counts as a jump (else a fall).")]
        public float JumpDetectVelocity = 0.5f;

        [Header("Pickup")]
        [Tooltip("Length (s) of the PickUp clip at speed 1. The clip is sped up/down to span the actual reach duration.")]
        public float PickupClipLength = 1.0f;

        [Header("Hand IK")]
        [Range(0f, 1f)] public float HandIkMaxWeight = 1f;
        [Tooltip("Seconds for the primary (pickup) hand to reach full IK weight.")]
        public float PrimaryHandReach = 0.18f;
        [Tooltip("Extra delay (s) before the second hand joins after pickup — gives a one-hand-grab to two-hand-hold feel.")]
        public float SecondHandDelay = 0.12f;
        [Tooltip("Seconds for hands to release (weight to 0).")]
        public float HandReleaseTime = 0.12f;
        [Tooltip("Rotation IK weight for hands (0 = position only, 1 = match the grip anchor's rotation).")]
        [Range(0f, 1f)] public float HandRotationWeight = 0.25f;
    }
}
