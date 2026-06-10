using UnityEngine;
using Gameplay.Player.Vitals;
using Gameplay.World.Weapon;

namespace Gameplay.World.Enemies.AI
{
    public struct ProbeHit { public Vector3 Point; public Vector3 Normal; public float Distance; }

    public struct WallInfo { public bool Found; public Vector3 Normal; public float Coverage; }

    public enum EnemyTargetKind { None, Cannon, Player }

    public struct EnemyTarget
    {
        public EnemyTargetKind Kind;
        public Transform Transform;
        public PlayerVitals Player;
        public bool IsValid => Kind != EnemyTargetKind.None && Transform != null;
        public Vector3 Position => Transform != null ? Transform.position : Vector3.zero;
    }

    public struct LatchInfo
    {
        public bool Active;
        public Transform Target;
        public Vector3 LocalOffset;
        public PlayerVitals Player;
        public WeaponSnapPoint Module;
    }

    public enum EnemyStateId { Pursue, Pounce, Latched, Dead }

    public interface ISurfaceProbe
    {
        bool Raycast(Vector3 origin, Vector3 dir, float dist, out ProbeHit hit);
    }

    public interface IEnemyState
    {
        EnemyStateId Id { get; }
        void Enter(EnemyContext ctx);
        EnemyStateId Tick(EnemyContext ctx, float dt);
        void Exit(EnemyContext ctx);
    }

    public enum EnemyEffectKind { None, Spawned, PrePounce, Pounced, Latched, Damaged, Died }
}
