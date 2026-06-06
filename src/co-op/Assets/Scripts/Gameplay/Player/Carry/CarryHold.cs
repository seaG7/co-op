using UnityEngine;

namespace Gameplay.Player.Carry
{

    public readonly struct HolderGrip
    {
        public readonly Vector3 GripPoint;
        public readonly Vector3 Forward;
        public readonly Vector3 Up;

        public HolderGrip(Vector3 gripPoint, Vector3 forward, Vector3 up)
        {
            GripPoint = gripPoint;
            Forward = forward;
            Up = up;
        }
    }

    public readonly struct CarryTarget
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public CarryTarget(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }
}
