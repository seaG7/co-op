using Data.Configs;
using UnityEngine;

namespace Gameplay.Player.Movement
{
    public sealed class MovementCalculator
    {
        private readonly MovementConfig _config;

        public MovementCalculator(MovementConfig config) => _config = config;

        public Vector3 ComputeVelocity(
            Vector3 currentVelocity,
            Vector2 moveInput,
            Transform playerTransform,
            bool isGrounded,
            float deltaTime)
        {
            var forward = Flatten(playerTransform.forward);
            var right = Flatten(playerTransform.right);
            var worldInput = right * moveInput.x + forward * moveInput.y;

            var inputMagnitude = Mathf.Clamp01(worldInput.magnitude);
            bool hasInput = inputMagnitude > 0.01f;

            var current = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            float air = isGrounded ? 1f : _config.AirControlCoefficient;

            Vector3 next;
            if (!hasInput)
            {
                next = Vector3.MoveTowards(current, Vector3.zero, _config.Deceleration * air * deltaTime);
            }
            else
            {
                var desiredDir = worldInput.normalized;
                var desiredVelocity = desiredDir * (_config.MoveSpeed * inputMagnitude);
                float curSpeed = current.magnitude;
                float align = curSpeed > 0.01f ? Vector3.Dot(current / curSpeed, desiredDir) : 1f;

                if (curSpeed > 0.5f && align < _config.TurnBrakeDot)
                    next = Vector3.MoveTowards(current, Vector3.zero, _config.TurnBrakeDeceleration * air * deltaTime);
                else
                    next = Vector3.MoveTowards(current, desiredVelocity, _config.Acceleration * air * deltaTime);
            }

            return new Vector3(next.x, currentVelocity.y, next.z);
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
        }
    }
}
