using FishNet.Connection;
using Gameplay.World.Items;
using UnityEngine;

namespace Infrastructure.Services.Carry
{
    public interface IPhysicalCarryService
    {
        bool TryGrab(Carryable item, NetworkConnection conn, Vector3 holderEye, Vector3 holderAim);

        void Release(Carryable item, NetworkConnection conn, Vector3 throwAim);

        bool IsHeldBy(Carryable item, NetworkConnection conn);

        int HolderCount(Carryable item);
    }
}
