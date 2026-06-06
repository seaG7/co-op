using FishNet.Object;
using Gameplay.Player.Carry;
using Gameplay.World.Items;
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
        [Inject] private IConfigDataProvider _configs;

        private MovementCalculator _calculator;
        private JumpController _jumpController;
        private GroundProbe _ground;

        private CharacterController _cc;
        private PlayerCarry _playerCarry;
        private Vector3 _velocity;
        private Vector3 _lastPosition;
        private bool _wasGrounded;
        private bool _jumpPressedThisFrame;
        private bool _inputBound;
        private bool _built;

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
            PlayerItemPhysics.RegisterPlayer(_cc);
            if (_configs == null)
            {
                Debug.LogError("[PlayerMovement] IConfigDataProvider not injected after spawn — runtime injection failed.");
                enabled = false;
                return;
            }
            if (base.IsOwner) BindInput();
        }

        public override void OnStopClient()
        {
            if (base.IsOwner) UnbindInput();
            PlayerItemPhysics.UnregisterPlayer(_cc);
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

        private bool EnsureServices()
        {
            if (_built) return true;
            var cfg = _configs?.Movement;
            if (cfg == null || _cc == null) return false;
            _calculator = new MovementCalculator(cfg);
            _jumpController = new JumpController(cfg);
            _ground = new GroundProbe(_cc, cfg, cfg.GroundMask);
            _built = true;
            return true;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (!EnsureServices()) return;

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
                    float mult = _configs.Carry.SpeedMultiplierForMass(_playerCarry.HeldMass);
                    _velocity.x *= mult;
                    _velocity.z *= mult;
                }

                _cc.Move(_velocity * dt);
            }
            else
            {
                _velocity = (transform.position - _lastPosition) / dt;
            }

            var localVel = transform.InverseTransformDirection(_velocity);
            Snapshot = new MovementSnapshot(
                localVel,
                new Vector2(localVel.x, localVel.z).magnitude,
                _ground.IsGrounded,
                !_wasGrounded && _ground.IsGrounded,
                _wasGrounded && !_ground.IsGrounded,
                jumpJustExecuted,
                _velocity.y,
                _ground.SlopeAngle);

            _wasGrounded = _ground.IsGrounded;
            _lastPosition = transform.position;
        }
    }
}
