using Data.Configs;
using Gameplay.Player.Carry;
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

        [Inject] private IConfigDataProvider _configs;

        private readonly IkWeightController _weights = new IkWeightController();

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_carry == null) _carry = GetComponentInParent<PlayerCarry>();
        }

        private void Update()
        {
            var c = _configs != null ? _configs.Animation : null;
            if (c == null || _carry == null) return;
            var held = _carry.CurrentHeld;
            bool holding = held != null;
            bool twoHands = holding && held.UsesTwoHands;
            _weights.Tick(holding, twoHands, Time.deltaTime,
                c.PrimaryHandReach, c.SecondHandDelay, c.HandReleaseTime, c.HandIkMaxWeight);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            var c = _configs != null ? _configs.Animation : null;
            if (_animator == null || c == null || _carry == null) return;
            _carry.PinForIK();
            var held = _carry.CurrentHeld;

            ApplyHand(AvatarIKGoal.RightHand, held != null ? held.HandGrip(HandSide.Right) : null, _weights.PrimaryWeight, c);
            ApplyHand(AvatarIKGoal.LeftHand, held != null ? held.HandGrip(HandSide.Left) : null, _weights.SecondaryWeight, c);
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
