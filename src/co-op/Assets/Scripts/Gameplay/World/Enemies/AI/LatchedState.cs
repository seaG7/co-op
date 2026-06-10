using UnityEngine;
using Gameplay.World.Weapon;

namespace Gameplay.World.Enemies.AI
{
    public sealed class LatchedState : IEnemyState
    {
        public EnemyStateId Id => EnemyStateId.Latched;

        public void Enter(EnemyContext ctx)
        {
            Transform t = ctx.Target.Transform;
            WeaponSnapPoint module = null;
            if (ctx.Target.Kind == EnemyTargetKind.Cannon)
            {
                module = NearestOccupiedModule(ctx.Body.position);
                if (module != null)
                {
                    t = module.transform;
                    ctx.Body.position = t.position + Vector3.up * ctx.Config.LatchHover;
                }
            }

            ctx.Latch = new LatchInfo
            {
                Active = true,
                Target = t,
                Player = ctx.Target.Player,
                Module = module,
                LocalOffset = t != null ? t.InverseTransformPoint(ctx.Body.position) : Vector3.zero
            };

            if (module != null) module.AddMob();
            if (ctx.Target.Kind == EnemyTargetKind.Player && ctx.Target.Player != null)
                ctx.PendingKnockdown = ctx.Target.Player;

            ctx.PendingEffect = EnemyEffectKind.Latched;
            ctx.PendingLatchOnPlayer = ctx.Target.Kind == EnemyTargetKind.Player;
        }

        public void Exit(EnemyContext ctx)
        {
            if (ctx.Latch.Module != null) ctx.Latch.Module.RemoveMob();
            ctx.Latch = default;
        }

        public EnemyStateId Tick(EnemyContext ctx, float dt)
        {
            if (ctx.DeadRequested) return EnemyStateId.Dead;
            if (ctx.Latch.Target == null) return EnemyStateId.Pursue;
            if (ctx.Latch.Module != null && !ctx.Latch.Module.IsOccupied.Value) return EnemyStateId.Pursue;

            Vector3 worldPos = ctx.Latch.Target.TransformPoint(ctx.Latch.LocalOffset);
            Vector3 up = worldPos - ctx.Latch.Target.position;
            up = up.sqrMagnitude < 1e-4f ? ctx.Up : up.normalized;

            Vector3 fwd = Vector3.ProjectOnPlane(ctx.Body.forward, up);
            fwd = fwd.sqrMagnitude < 1e-4f ? ctx.Forward : fwd.normalized;

            ctx.Up = up;
            ctx.Forward = fwd;
            ctx.Body.SetPositionAndRotation(worldPos, Quaternion.LookRotation(fwd, up));
            return Id;
        }

        private static WeaponSnapPoint NearestOccupiedModule(Vector3 pos)
        {
            var all = WeaponSnapPoint.All;
            WeaponSnapPoint best = null;
            float bestSq = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                if (m == null || !m.IsOccupied.Value) continue;
                float sq = (m.transform.position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = m; }
            }
            return best;
        }
    }
}
