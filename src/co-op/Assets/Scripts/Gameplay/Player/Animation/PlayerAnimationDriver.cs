using Gameplay.Player.Movement;
using UnityEngine;

namespace Gameplay.Player.Animation
{
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerMovement _movement;

        [Header("Animator parameter names")]
        [SerializeField] private string _speedParam = "Speed";
        [SerializeField] private string _localVelXParam = "LocalVelX";
        [SerializeField] private string _localVelZParam = "LocalVelZ";
        [SerializeField] private string _verticalVelocityParam = "VerticalVelocity";
        [SerializeField] private string _isGroundedParam = "IsGrounded";
        [SerializeField] private string _jumpTriggerParam = "JumpTrigger";
        [SerializeField] private string _landTriggerParam = "LandTrigger";

        private int _speedHash, _xHash, _zHash, _vyHash, _groundedHash, _jumpHash, _landHash;
        private bool _hashed;

        [SerializeField] private float _maxSpeedForNormalization = 5.5f;

        private void Awake()
        {
            if (_movement == null) _movement = GetComponentInParent<PlayerMovement>() ?? GetComponent<PlayerMovement>();
        }

        private void OnEnable() => EnsureHashes();

        private void Update()
        {
            if (_animator == null || _movement == null) return;
            EnsureHashes();

            var s = _movement.Snapshot;
            var inv = _maxSpeedForNormalization > 0f ? 1f / _maxSpeedForNormalization : 0f;

            _animator.SetFloat(_speedHash, Mathf.Clamp01(s.HorizontalSpeed * inv));
            _animator.SetFloat(_xHash, Mathf.Clamp(s.LocalVelocity.x * inv, -1f, 1f));
            _animator.SetFloat(_zHash, Mathf.Clamp(s.LocalVelocity.z * inv, -1f, 1f));
            _animator.SetFloat(_vyHash, s.VerticalVelocity);
            _animator.SetBool(_groundedHash, s.IsGrounded);

            if (s.JumpJustExecuted) _animator.SetTrigger(_jumpHash);
            if (s.WasJustGrounded) _animator.SetTrigger(_landHash);
        }

        private void EnsureHashes()
        {
            if (_hashed) return;
            _speedHash = Animator.StringToHash(_speedParam);
            _xHash = Animator.StringToHash(_localVelXParam);
            _zHash = Animator.StringToHash(_localVelZParam);
            _vyHash = Animator.StringToHash(_verticalVelocityParam);
            _groundedHash = Animator.StringToHash(_isGroundedParam);
            _jumpHash = Animator.StringToHash(_jumpTriggerParam);
            _landHash = Animator.StringToHash(_landTriggerParam);
            _hashed = true;
        }
    }
}
