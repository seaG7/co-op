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
            var desiredVelocity = inputMagnitude > 0.001f
                ? worldInput.normalized * (_config.MoveSpeed * inputMagnitude)
                : Vector3.zero;

            var rate = inputMagnitude > 0.01f ? _config.Acceleration : _config.Deceleration;
            if (!isGrounded) rate *= _config.AirControlCoefficient;

            var current = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            var next = Vector3.MoveTowards(current, desiredVelocity, rate * deltaTime);
            return new Vector3(next.x, currentVelocity.y, next.z);
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
        }
    }
}
