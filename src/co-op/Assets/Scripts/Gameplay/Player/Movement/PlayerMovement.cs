using FishNet.Object;
using Gameplay.Player.Carry;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Input;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Movement
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Inject] private IInputService _input;
        [Inject] private MovementCalculator _calculator;
        [Inject] private JumpController _jumpController;
        [Inject] private GroundProbe _ground;
        [Inject] private IConfigDataProvider _configs;

        private CharacterController _cc;
        private PlayerCarry _playerCarry;
        private Vector3 _velocity;
        private Vector3 _lastPosition;
        private bool _wasGrounded;
        private bool _jumpPressedThisFrame;
        private bool _inputBound;

        public MovementSnapshot Snapshot { get; private set; }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _playerCarry = GetComponent<PlayerCarry>();
            _lastPosition = transform.position;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner) BindInput();
        }

        public override void OnStopClient()
        {
            if (base.IsOwner) UnbindInput();
            base.OnStopClient();
        }

        private void OnDestroy() => UnbindInput();

        private void BindInput()
        {
            if (_inputBound || _input == null) return;
            _input.JumpStarted += OnJumpStarted;
            _inputBound = true;
        }

        private void UnbindInput()
        {
            if (!_inputBound || _input == null) return;
            _input.JumpStarted -= OnJumpStarted;
            _inputBound = false;
        }

        private void OnJumpStarted() => _jumpPressedThisFrame = true;

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            _ground.Tick();

            bool jumpJustExecuted = false;

            if (base.IsOwner)
            {
                _velocity = _calculator.ComputeVelocity(
                    _velocity,
                    _input != null ? _input.MoveAxis : Vector2.zero,
                    transform, _ground.IsGrounded, dt);

                _jumpController.Tick(_jumpPressedThisFrame, _ground.IsGrounded, dt);
                var jumpHeld = _input != null && _input.JumpHeld;
                _velocity.y = _jumpController.ProcessVertical(_velocity.y, _ground.IsGrounded, jumpHeld, dt);
                _jumpPressedThisFrame = false;
                jumpJustExecuted = _jumpController.JumpedThisFrame;

                if (_playerCarry != null && _playerCarry.IsHolding && _configs?.Carry != null)
                {
                    var cc = _configs.Carry;
                    float excess = Mathf.Max(0f, _playerCarry.HeldMass - cc.FreeCarryMass);
                    if (excess > 0f)
                    {
                        float mult = Mathf.Max(cc.MinSpeedMultiplier, 1f / (1f + excess * cc.MovementSlowdownPerKg));
                        _velocity.x *= mult;
                        _velocity.z *= mult;
                    }
                }

                _cc.Move(_velocity * dt);
            }
            else
            {
                _velocity = (transform.position - _lastPosition) / dt;
            }

            var localVel = transform.InverseTransformDirection(_velocity);
            Snapshot = new MovementSnapshot(
                localVelocity: localVel,
                horizontalSpeed: new Vector2(localVel.x, localVel.z).magnitude,
                isGrounded: _ground.IsGrounded,
                wasJustGrounded: !_wasGrounded && _ground.IsGrounded,
                wasJustAirborne: _wasGrounded && !_ground.IsGrounded,
                jumpJustExecuted: jumpJustExecuted,
                verticalVelocity: _velocity.y,
                slopeAngle: _ground.SlopeAngle);

            _wasGrounded = _ground.IsGrounded;
            _lastPosition = transform.position;
        }
    }
}
