using Cysharp.Threading.Tasks;
using FishNet.Object;
using Gameplay.Player.Carry;
using Gameplay.World.Items;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Input;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Movement
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Inject] private IInputService _input;
        [Inject] private IConfigDataProvider _configs;
        [Inject] private SignalBus _signalBus;

        private MovementCalculator _calculator;
        private JumpController _jumpController;
        private GroundProbe _ground;

        private CharacterController _cc;
        private PlayerCarry _playerCarry;
        private Vector3 _velocity;
        private Vector3 _moveVel;
        private Vector3 _lastPosition;
        private bool _wasGrounded;
        private bool _jumpPressedThisFrame;
        private bool _inputBound;
        private bool _built;

        public MovementSnapshot Snapshot { get; private set; }

        private readonly StepCadence _cadence = new StepCadence();
        private float _lastVerticalVel;

        public float StepPhase => _cadence.Phase;
        public Vector3 WorldVelocity => _moveVel;

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
                enabled = false;
                EnableWhenReadyAsync().Forget();
                return;
            }
            if (base.IsOwner) BindInput();
        }

        private async UniTaskVoid EnableWhenReadyAsync()
        {
            for (int i = 0; i < 900 && _configs == null; i++)
                await UniTask.Yield(PlayerLoopTiming.Update);

            if (this == null) return;
            if (_configs == null)
            {
                Debug.LogError("[PlayerMovement] IConfigDataProvider not injected after spawn — runtime injection failed.");
                return;
            }

            enabled = true;
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

            var cfg = _configs.Movement;
            _ground.Tick();

            bool jumpJustExecuted = false;

            Vector3 moveVel;
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

                float baseHorizontal = new Vector2(_velocity.x, _velocity.z).magnitude;
                _cadence.Tick(baseHorizontal, _ground.IsGrounded, cfg.StepLength, cfg.StepMinSpeed, dt);

                // mass + gait scale ONLY the move vector — never fed back into _velocity
                float scale = 1f;
                if (_playerCarry != null && _playerCarry.IsHolding && _configs?.Carry != null)
                    scale *= _configs.Carry.SpeedMultiplierForMass(_playerCarry.HeldMass);
                scale *= _cadence.SpeedMultiplier(cfg.GaitSpeedAmplitude);

                moveVel = new Vector3(_velocity.x * scale, _velocity.y, _velocity.z * scale);
                _cc.Move(moveVel * dt);
            }
            else
            {
                _velocity = (transform.position - _lastPosition) / dt;
                moveVel = _velocity;
                float observed = new Vector2(_velocity.x, _velocity.z).magnitude;
                _cadence.Tick(observed, _ground.IsGrounded, cfg.StepLength, cfg.StepMinSpeed, dt);
            }

            _moveVel = moveVel;

            var localVel = transform.InverseTransformDirection(moveVel);
            bool justLanded = !_wasGrounded && _ground.IsGrounded;
            Snapshot = new MovementSnapshot(
                localVel,
                new Vector2(localVel.x, localVel.z).magnitude,
                _ground.IsGrounded,
                justLanded,
                _wasGrounded && !_ground.IsGrounded,
                jumpJustExecuted,
                moveVel.y,
                _ground.SlopeAngle);

            if (_signalBus != null)
            {
                if (_cadence.FootfallThisTick)
                    _signalBus.Fire(new PlayerFootstepSignal(transform.position, _cadence.IsLeftFoot));
                if (justLanded)
                    _signalBus.Fire(new PlayerLandedSignal(transform.position, Mathf.Abs(_lastVerticalVel)));
            }

            _wasGrounded = _ground.IsGrounded;
            _lastVerticalVel = moveVel.y;
            _lastPosition = transform.position;
        }
    }
}
