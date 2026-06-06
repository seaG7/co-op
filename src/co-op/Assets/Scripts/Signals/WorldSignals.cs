using UnityEngine;

namespace Signals
{
    public readonly struct LevelReadySignal { }

    public readonly struct ItemImpactSignal
    {
        public readonly Vector3 Point;
        public readonly float Impulse;
        public ItemImpactSignal(Vector3 point, float impulse) { Point = point; Impulse = impulse; }
    }

    public readonly struct SourceVulnerableSignal
    {
        public readonly bool Vulnerable;
        public SourceVulnerableSignal(bool vulnerable) { Vulnerable = vulnerable; }
    }

    public readonly struct SourceDamagedSignal
    {
        public readonly float Health;
        public readonly float MaxHealth;
        public SourceDamagedSignal(float health, float maxHealth) { Health = health; MaxHealth = maxHealth; }
    }

    public readonly struct SourceDestroyedSignal { }

    public readonly struct WeaponFiredSignal
    {
        public readonly Vector3 Origin;
        public readonly Vector3 HitPoint;
        public readonly bool Hit;
        public WeaponFiredSignal(Vector3 origin, Vector3 hitPoint, bool hit)
        {
            Origin = origin;
            HitPoint = hitPoint;
            Hit = hit;
        }
    }
}
