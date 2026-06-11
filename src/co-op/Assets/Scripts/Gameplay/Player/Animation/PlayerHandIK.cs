using Data.Configs;
using Gameplay.Player.Carry;
using Gameplay.Player.Weapons;
using Infrastructure.Providers.Configs;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Animation
{
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerHandIK : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerCarry _carry;
        [SerializeField] private PlayerWeaponControl _weaponControl;

        [Inject] private IConfigDataProvider _configs;

        private readonly IkWeightController _weights = new IkWeightController();
        private float _mountWeight;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_carry == null) _carry = GetComponentInParent<PlayerCarry>();
            if (_weaponControl == null) _weaponControl = GetComponentInParent<PlayerWeaponControl>();
        }

        private bool MountedOnCannon =>
            _weaponControl != null && _weaponControl.IsMounted && _weaponControl.MountGripRight != null;

        private void Update()
        {
            var c = _configs != null ? _configs.Animation : null;
            if (c == null) return;

            bool mounted = MountedOnCannon;
            float reach = Mathf.Max(0.01f, c.PrimaryHandReach);
            _mountWeight = Mathf.MoveTowards(_mountWeight, mounted ? c.HandIkMaxWeight : 0f, Time.deltaTime / reach);

            if (_carry == null) return;
            var held = _carry.CurrentHeld;
            bool holding = held != null && !mounted;
            bool twoHands = holding && held.UsesTwoHands;
            _weights.Tick(holding, twoHands, Time.deltaTime,
                c.PrimaryHandReach, c.SecondHandDelay, c.HandReleaseTime, c.HandIkMaxWeight);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            var c = _configs != null ? _configs.Animation : null;
            if (_animator == null || c == null) return;

            if (_mountWeight > 0.01f && _weaponControl != null && _weaponControl.IsMounted)
            {
                ApplyHand(AvatarIKGoal.RightHand, _weaponControl.MountGripRight, _mountWeight, c);
                ApplyHand(AvatarIKGoal.LeftHand, _weaponControl.MountGripLeft, _mountWeight, c);
                ApplyHint(AvatarIKHint.RightElbow, null, 0f);
                ApplyHint(AvatarIKHint.LeftElbow, null, 0f);
                return;
            }

            if (_carry == null) return;
            _carry.PinForIK();
            var held = _carry.CurrentHeld;

            ApplyHand(AvatarIKGoal.RightHand, held != null ? held.HandGrip(HandSide.Right) : null, _weights.PrimaryWeight, c);
            ApplyHand(AvatarIKGoal.LeftHand, held != null ? held.HandGrip(HandSide.Left) : null, _weights.SecondaryWeight, c);

            ApplyHint(AvatarIKHint.RightElbow, held != null ? held.ElbowHint(HandSide.Right) : null, _weights.PrimaryWeight);
            ApplyHint(AvatarIKHint.LeftElbow, held != null ? held.ElbowHint(HandSide.Left) : null, _weights.SecondaryWeight);
        }

        private void ApplyHint(AvatarIKHint hint, Transform target, float weight)
        {
            if (target == null || weight <= 0f)
            {
                _animator.SetIKHintPositionWeight(hint, 0f);
                return;
            }
            _animator.SetIKHintPositionWeight(hint, weight);
            _animator.SetIKHintPosition(hint, target.position);
        }

        private void ApplyHand(AvatarIKGoal goal, Transform target, float weight, AnimationConfig c)
        {
            if (target == null || weight <= 0f)
            {
                _animator.SetIKPositionWeight(goal, 0f);
                _animator.SetIKRotationWeight(goal, 0f);
                return;
            }
            _animator.SetIKPositionWeight(goal, weight);
            _animator.SetIKRotationWeight(goal, weight * c.HandRotationWeight);
            _animator.SetIKPosition(goal, target.position);
            _animator.SetIKRotation(goal, target.rotation);
        }
    }
}
