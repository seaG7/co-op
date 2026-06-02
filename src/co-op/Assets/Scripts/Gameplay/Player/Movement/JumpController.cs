using Data.Configs;
using UnityEngine;

namespace Gameplay.Player.Movement
{
    public sealed class JumpController
    {
        private readonly MovementConfig _config;

        private float _coyoteTimeLeft;
        private float _jumpBufferLeft;
        private bool _isAscending;
        private bool _variableCutAvailable;

        public bool JumpedThisFrame { get; private set; }

        public JumpController(MovementConfig config) => _config = config;

        public void Tick(bool jumpStartedThisFrame, bool isGrounded, float deltaTime)
        {
            if (isGrounded) _coyoteTimeLeft = _config.CoyoteTimeSec;
            else _coyoteTimeLeft = Mathf.Max(0f, _coyoteTimeLeft - deltaTime);

            if (jumpStartedThisFrame) _jumpBufferLeft = _config.JumpBufferSec;
            else _jumpBufferLeft = Mathf.Max(0f, _jumpBufferLeft - deltaTime);
        }

        public float ProcessVertical(float verticalVelocity, bool isGrounded, bool jumpHeld, float deltaTime)
        {
            JumpedThisFrame = false;

            if (_jumpBufferLeft > 0f && _coyoteTimeLeft > 0f)
            {
                verticalVelocity = ComputeJumpImpulse();
                _jumpBufferLeft = 0f;
                _coyoteTimeLeft = 0f;
                _isAscending = true;
                _variableCutAvailable = true;
                JumpedThisFrame = true;
            }

            if (_variableCutAvailable && !jumpHeld && verticalVelocity > 0f)
            {
                verticalVelocity *= _config.VariableJumpCutoff;
                _variableCutAvailable = false;
            }

            if (verticalVelocity > 0f)
            {
                _isAscending = true;
                var g = _config.Gravity;
                if (Mathf.Abs(verticalVelocity) < _config.ApexThreshold)
                    g *= _config.ApexGravityMultiplier;
                verticalVelocity -= g * deltaTime;
            }
            else
            {
                _isAscending = false;
                _variableCutAvailable = false;
                verticalVelocity -= _config.Gravity * _config.FallGravityMultiplier * deltaTime;
            }

            verticalVelocity = Mathf.Max(verticalVelocity, -_config.MaxFallSpeed);

            if (isGrounded && verticalVelocity < 0f && !JumpedThisFrame)
                verticalVelocity = -2f;

            return verticalVelocity;
        }

        private float ComputeJumpImpulse()
        {
            return Mathf.Sqrt(2f * _config.Gravity * _config.JumpHeight);
        }
    }
}
