using Data.Configs;
using Gameplay.Player.Carry;
using Gameplay.Player.Movement;
using Gameplay.Player.Vitals;
using Infrastructure.Providers.Configs;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Animation
{
    public sealed class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerCarry _carry;
        [SerializeField] private PlayerVitals _vitals;

        [Inject] private IConfigDataProvider _configs;

        private AnimationConfig Cfg => _configs != null ? _configs.Animation : null;

        private int _hSpeed, _hVx, _hVz, _hVy, _hGrounded, _hCarrying, _hDowned, _hJump, _hLand, _hPickup, _hPickupSpeed, _hGettingUp, _hTurn, _hKick, _hDrinking, _hDrunk;
        private bool _hashed;
        private bool _prevGrounded = true;
        private bool _prevCarrying;
        private bool _prevDowned;
        private float _prevYaw;
        private float _turnValue;
        private PlayerDrunk _drunk;

        private void Awake()
        {
            if (_movement == null) _movement = GetComponentInParent<PlayerMovement>();
            if (_carry == null) _carry = GetComponentInParent<PlayerCarry>();
            if (_vitals == null) _vitals = GetComponentInParent<PlayerVitals>();
            if (_drunk == null) _drunk = GetComponentInParent<PlayerDrunk>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            _prevYaw = transform.eulerAngles.y;
        }

        public void TriggerKick()
        {
            if (_animator == null) return;
            EnsureHashes();
            _animator.SetTrigger(_hKick);
        }

        public void SetDrinking(bool drinking)
        {
            if (_animator == null) return;
            EnsureHashes();
            _animator.SetBool(_hDrinking, drinking);
        }

        private void EnsureHashes()
        {
            if (_hashed) return;
            var c = Cfg;
            if (c == null) return;
            _hSpeed = Animator.StringToHash(c.SpeedParam);
            _hVx = Animator.StringToHash(c.LocalVelXParam);
            _hVz = Animator.StringToHash(c.LocalVelZParam);
            _hVy = Animator.StringToHash(c.VerticalVelocityParam);
            _hGrounded = Animator.StringToHash(c.IsGroundedParam);
            _hCarrying = Animator.StringToHash(c.IsCarryingParam);
            _hDowned = Animator.StringToHash(c.IsDownedParam);
            _hJump = Animator.StringToHash(c.JumpTrigger);
            _hLand = Animator.StringToHash(c.LandTrigger);
            _hPickup = Animator.StringToHash(c.PickUpTrigger);
            _hPickupSpeed = Animator.StringToHash(c.PickUpSpeedParam);
            _hGettingUp = Animator.StringToHash(c.GettingUpTrigger);
            _hTurn = Animator.StringToHash(c.TurnParam);
            _hKick = Animator.StringToHash(c.KickTrigger);
            _hDrinking = Animator.StringToHash(c.DrinkingParam);
            _hDrunk = Animator.StringToHash(c.IsDrunkParam);
            _hashed = true;
        }

        private void Update()
        {
            if (_animator == null || _movement == null) return;
            var c = Cfg;
            if (c == null) return;
            EnsureHashes();

            var s = _movement.Snapshot;
            float dt = Time.deltaTime;
            float inv = c.MaxSpeedForNormalization > 0f ? 1f / c.MaxSpeedForNormalization : 0f;

            _animator.SetFloat(_hSpeed, Mathf.Clamp01(s.HorizontalSpeed * inv), c.LocomotionDampTime, dt);
            _animator.SetFloat(_hVx, Mathf.Clamp(s.LocalVelocity.x * inv, -1f, 1f), c.LocomotionDampTime, dt);
            _animator.SetFloat(_hVz, Mathf.Clamp(s.LocalVelocity.z * inv, -1f, 1f), c.LocomotionDampTime, dt);
            _animator.SetFloat(_hVy, s.VerticalVelocity);
            _animator.SetBool(_hGrounded, s.IsGrounded);

            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(_prevYaw, yaw) / dt;
            _prevYaw = yaw;
            float turnTarget = Mathf.Clamp(yawRate / Mathf.Max(1f, c.TurnRateForFull), -1f, 1f);
            _turnValue = Mathf.Lerp(_turnValue, turnTarget, 1f - Mathf.Exp(-(1f / Mathf.Max(0.01f, c.TurnDamp)) * dt));
            _animator.SetFloat(_hTurn, _turnValue);

            var d = AnimatorStateResolver.Evaluate(
                _prevGrounded, s.IsGrounded, s.VerticalVelocity, c.JumpDetectVelocity);
            if (d.FireJump) _animator.SetTrigger(_hJump);
            if (d.FireLand) _animator.SetTrigger(_hLand);
            _prevGrounded = s.IsGrounded;

            bool carrying = _carry != null && _carry.CurrentHeld != null;
            _animator.SetBool(_hCarrying, carrying);
            if (carrying && !_prevCarrying)
            {
                float dur = _carry != null ? Mathf.Max(0.05f, _carry.PickupDuration) : 0.3f;
                _animator.SetFloat(_hPickupSpeed, c.PickupClipLength / dur);
                _animator.SetTrigger(_hPickup);
            }
            _prevCarrying = carrying;

            bool downed = _vitals != null && _vitals.IsDowned;
            _animator.SetBool(_hDowned, downed);
            if (_prevDowned && !downed && (_vitals == null || _vitals.IsAlive))
                _animator.SetTrigger(_hGettingUp);
            _prevDowned = downed;

            if (_drunk != null) _animator.SetBool(_hDrunk, _drunk.IsDrunk);
        }
    }
}
