using UnityEngine;

namespace Gameplay.Player.Movement
{
    public readonly struct MovementSnapshot
    {
        public readonly Vector3 LocalVelocity;
        public readonly float HorizontalSpeed;
        public readonly bool IsGrounded;
        public readonly bool WasJustGrounded;
        public readonly bool WasJustAirborne;
        public readonly bool JumpJustExecuted;
        public readonly float VerticalVelocity;
        public readonly float SlopeAngle;

        public MovementSnapshot(
            Vector3 localVelocity,
            float horizontalSpeed,
            bool isGrounded,
            bool wasJustGrounded,
            bool wasJustAirborne,
            bool jumpJustExecuted,
            float verticalVelocity,
            float slopeAngle)
        {
            LocalVelocity = localVelocity;
            HorizontalSpeed = horizontalSpeed;
            IsGrounded = isGrounded;
            WasJustGrounded = wasJustGrounded;
            WasJustAirborne = wasJustAirborne;
            JumpJustExecuted = jumpJustExecuted;
            VerticalVelocity = verticalVelocity;
            SlopeAngle = slopeAngle;
        }
    }
}
