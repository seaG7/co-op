using UnityEngine;

namespace Gameplay.Player.Movement
{
    // Distance-driven step phase: the player advances "per step" rather than gliding at a flat
    // speed. Phase in [0,1) per stride; two footfalls per stride (left/right). Pure C#, runs on
    // every client from that client's observed horizontal speed.
    public sealed class StepCadence
    {
        public float Phase { get; private set; }
        public bool FootfallThisTick { get; private set; }
        public bool IsLeftFoot { get; private set; }

        private int _lastHalf = -1;

        public void Tick(float horizontalSpeed, bool grounded, float stepLength, float minSpeed, float dt)
        {
            FootfallThisTick = false;
            if (!grounded || horizontalSpeed < minSpeed || stepLength <= 0f || dt <= 0f)
            {
                _lastHalf = -1;
                return;
            }

            Phase += (horizontalSpeed / stepLength) * dt;
            Phase -= Mathf.Floor(Phase);

            int half = Phase < 0.5f ? 0 : 1;
            if (half != _lastHalf)
            {
                if (_lastHalf != -1) { FootfallThisTick = true; IsLeftFoot = half == 1; }
                _lastHalf = half;
            }
        }

        // ~1.0, oscillates per step (two waves per stride). amplitude 0 -> constant speed.
        public float SpeedMultiplier(float amplitude) => 1f + amplitude * Mathf.Sin(Phase * Mathf.PI * 4f);

        public float BobVertical() => Mathf.Sin(Phase * Mathf.PI * 4f); // 2 dips per stride (one per foot)
        public float BobLateral() => Mathf.Sin(Phase * Mathf.PI * 2f);  // 1 sway per stride
    }
}
