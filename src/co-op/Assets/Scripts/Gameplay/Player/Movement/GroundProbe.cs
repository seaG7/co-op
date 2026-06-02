using Data.Configs;
using UnityEngine;

namespace Gameplay.Player.Movement
{
    public sealed class GroundProbe
    {
        private readonly CharacterController _cc;
        private readonly MovementConfig _config;
        private readonly LayerMask _groundMask;

        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float SlopeAngle { get; private set; }

        public GroundProbe(CharacterController cc, MovementConfig config, LayerMask groundMask)
        {
            _cc = cc;
            _config = config;
            _groundMask = groundMask;
        }

        public void Tick()
        {
            if (_cc == null)
            {
                IsGrounded = false;
                return;
            }

            var radius = Mathf.Max(_cc.radius * 0.9f, 0.05f);
            const float startLift = 0.1f;
            var center = _cc.transform.position + _cc.center;
            var feetY = center.y - _cc.height * 0.5f;
            var origin = new Vector3(center.x, feetY + radius + startLift, center.z);
            var distance = startLift + _config.GroundProbeDistance + _cc.skinWidth;

            if (Physics.SphereCast(origin, radius, Vector3.down, out var hit,
                    distance, _groundMask, QueryTriggerInteraction.Ignore))
            {
                var angle = Vector3.Angle(hit.normal, Vector3.up);
                if (angle <= _config.MaxSlopeAngle)
                {
                    IsGrounded = true;
                    GroundNormal = hit.normal;
                    SlopeAngle = angle;
                    return;
                }
            }

            IsGrounded = false;
            GroundNormal = Vector3.up;
            SlopeAngle = 0f;
        }
    }
}
