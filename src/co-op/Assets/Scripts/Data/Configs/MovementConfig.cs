using UnityEngine;

namespace Data.Configs
{
    [CreateAssetMenu(menuName = "Configs/Movement Config", fileName = "MovementConfig")]
    public sealed class MovementConfig : ScriptableObject
    {
        [Header("Horizontal")]
        [Tooltip("Top horizontal speed (m/s). One speed — no walk/sprint.")]
        public float MoveSpeed = 5.5f;

        [Tooltip("Acceleration on ground (m/s²). Lower = you build up speed (idle->walk->run) instead of snapping to full speed.")]
        public float Acceleration = 16f;

        [Tooltip("Deceleration on ground when input is zero (m/s²). Higher = tighter stop.")]
        public float Deceleration = 30f;

        [Tooltip("Braking (m/s²) when input sharply opposes current velocity — kills speed to ~0 before rebuilding the other way (momentum on 180° / left<->right reversals).")]
        public float TurnBrakeDeceleration = 45f;

        [Tooltip("Dot(currentDir, inputDir) below which a turn counts as a sharp reversal and brakes first. -0.2 ≈ turning more than ~100°.")]
        [Range(-1f, 1f)] public float TurnBrakeDot = -0.2f;

        [Tooltip("Multiplier on accel/decel while airborne. 0 = no air control, 1 = full.")]
        [Range(0f, 1f)]
        public float AirControlCoefficient = 0.55f;

        [Header("Vertical / Gravity")]
        [Tooltip("Base gravity magnitude (m/s²).")]
        public float Gravity = 25f;

        [Tooltip("Multiplier applied while falling — heavier fall feels snappier.")]
        public float FallGravityMultiplier = 1.8f;

        [Tooltip("Multiplier near jump apex when |verticalVelocity| < ApexThreshold. <1 gives hang time.")]
        [Range(0.1f, 1f)]
        public float ApexGravityMultiplier = 0.55f;

        [Tooltip("|verticalVelocity| under this counts as 'near apex' for the apex modifier.")]
        public float ApexThreshold = 2f;

        [Tooltip("Maximum downward speed (m/s).")]
        public float MaxFallSpeed = 40f;

        [Header("Jump")]
        [Tooltip("Target jump height in meters (initial impulse computed from gravity).")]
        public float JumpHeight = 1.6f;

        [Tooltip("On Jump release while ascending, vertical velocity is multiplied by this. Enables variable jump height.")]
        [Range(0f, 1f)]
        public float VariableJumpCutoff = 0.4f;

        [Tooltip("Time after leaving ground during which a jump is still allowed (seconds).")]
        public float CoyoteTimeSec = 0.12f;

        [Tooltip("Time before landing during which a Jump press is buffered and triggers on land (seconds).")]
        public float JumpBufferSec = 0.12f;

        [Header("Ground probe")]
        [Tooltip("Additional ray distance beyond CC radius for ground SphereCast.")]
        public float GroundProbeDistance = 0.15f;

        [Tooltip("Layers treated as ground for the grounded SphereCast.")]
        public LayerMask GroundMask = ~0;

        [Tooltip("Slope angle (degrees) above which the player slides instead of being grounded.")]
        [Range(0f, 89f)]
        public float MaxSlopeAngle = 50f;

        [Header("Step feel")]
        [Tooltip("Distance (m) covered per footstep. Drives the step cadence, camera bob and footstep timing.")]
        public float StepLength = 1.9f;

        [Tooltip("Per-step horizontal speed modulation (0 = constant glide; ~0.12 = you feel discrete steps).")]
        [Range(0f, 0.5f)] public float GaitSpeedAmplitude = 0.12f;

        [Tooltip("Minimum horizontal speed (m/s) that counts as moving for footsteps and camera bob.")]
        public float StepMinSpeed = 0.3f;
    }
}
