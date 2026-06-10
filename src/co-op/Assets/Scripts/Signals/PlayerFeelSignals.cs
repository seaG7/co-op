using UnityEngine;

namespace Signals
{
    public readonly struct PlayerFootstepSignal
    {
        public readonly Vector3 Position;
        public readonly bool IsLeft;
        public PlayerFootstepSignal(Vector3 position, bool isLeft)
        {
            Position = position;
            IsLeft = isLeft;
        }
    }

    public readonly struct PlayerLandedSignal
    {
        public readonly Vector3 Position;
        public readonly float Impact;
        public PlayerLandedSignal(Vector3 position, float impact)
        {
            Position = position;
            Impact = impact;
        }
    }

    public readonly struct PlayerDrunkSignal
    {
        public readonly bool IsOwner;
        public readonly float Intensity;
        public PlayerDrunkSignal(bool isOwner, float intensity)
        {
            IsOwner = isOwner;
            Intensity = intensity;
        }
    }
}
