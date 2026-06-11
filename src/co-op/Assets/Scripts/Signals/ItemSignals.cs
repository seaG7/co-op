using UnityEngine;

namespace Signals
{
    public readonly struct ItemPickedUpSignal
    {
        public readonly Vector3 Position;
        public ItemPickedUpSignal(Vector3 position) { Position = position; }
    }

    public readonly struct ItemThrownSignal
    {
        public readonly Vector3 Position;
        public ItemThrownSignal(Vector3 position) { Position = position; }
    }

    public readonly struct ItemSnappedSignal
    {
        public readonly Vector3 Position;
        public ItemSnappedSignal(Vector3 position) { Position = position; }
    }

    public readonly struct CorpseHeldSignal
    {
        public readonly bool Holding;
        public CorpseHeldSignal(bool holding) { Holding = holding; }
    }

    public readonly struct WeaponAssembledSignal
    {
        public readonly Vector3 Position;
        public WeaponAssembledSignal(Vector3 position) { Position = position; }
    }

    public readonly struct CannonChargeChangedSignal
    {
        public readonly int Loaded;
        public readonly int Required;
        public bool IsCharged => Required <= 0 || Loaded >= Required;
        public CannonChargeChangedSignal(int loaded, int required) { Loaded = loaded; Required = required; }
    }

    public readonly struct CannonModuleState
    {
        public readonly int Order;
        public readonly bool Assembled;
        public readonly int MobCount;
        public CannonModuleState(int order, bool assembled, int mobCount)
        {
            Order = order; Assembled = assembled; MobCount = mobCount;
        }
    }

    public readonly struct CannonModulesChangedSignal
    {
        public readonly CannonModuleState[] Modules;
        public readonly int Assembled;
        public readonly int UnderAttack;
        public readonly int Detached;
        public readonly int Total;
        public CannonModulesChangedSignal(CannonModuleState[] modules, int assembled, int underAttack, int detached, int total)
        {
            Modules = modules; Assembled = assembled; UnderAttack = underAttack; Detached = detached; Total = total;
        }
    }

    public readonly struct ModuleDetachedSignal
    {
        public readonly Vector3 Position;
        public readonly int Order;
        public ModuleDetachedSignal(Vector3 position, int order) { Position = position; Order = order; }
    }
}
