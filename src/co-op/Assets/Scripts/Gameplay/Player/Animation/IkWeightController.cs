using UnityEngine;

namespace Gameplay.Player.Animation
{
    public sealed class IkWeightController
    {
        public float PrimaryWeight { get; private set; }
        public float SecondaryWeight { get; private set; }

        private float _holdTime;

        public void Tick(bool holding, bool twoHands, float dt,
                         float reachTime, float secondDelay, float releaseTime, float maxWeight)
        {
            if (holding)
            {
                _holdTime += dt;
                PrimaryWeight = Approach(PrimaryWeight, maxWeight, dt, reachTime);
                float secondTarget = (twoHands && _holdTime >= secondDelay) ? maxWeight : 0f;
                SecondaryWeight = Approach(SecondaryWeight, secondTarget, dt, reachTime);
            }
            else
            {
                _holdTime = 0f;
                PrimaryWeight = Approach(PrimaryWeight, 0f, dt, releaseTime);
                SecondaryWeight = Approach(SecondaryWeight, 0f, dt, releaseTime);
            }
        }

        private static float Approach(float current, float target, float dt, float time)
        {
            if (time <= 0f) return target;
            return Mathf.MoveTowards(current, target, dt / time);
        }
    }
}
