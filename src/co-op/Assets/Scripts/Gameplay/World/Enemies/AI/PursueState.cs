using UnityEngine;

namespace Gameplay.World.Enemies.AI
{
    public sealed class PursueState : IEnemyState
    {
        public EnemyStateId Id => EnemyStateId.Pursue;
        public void Enter(EnemyContext ctx) { }
        public void Exit(EnemyContext ctx) { }

        public EnemyStateId Tick(EnemyContext ctx, float dt)
        {
            if (ctx.DeadRequested) return EnemyStateId.Dead;
            if (ctx.PounceCooldownLeft > 0f) ctx.PounceCooldownLeft -= dt;

            ctx.Target = ctx.Targeting.Resolve(ctx.Body.position, ctx.Config, ctx.Target);
            if (!ctx.Target.IsValid) return Id;

            Vector3 goal = ctx.Target.Position - ctx.Body.position;
            float dist = goal.magnitude;

            if (ctx.DetourTimer > 0f)
            {
                ctx.DetourTimer -= dt;
                goal = Quaternion.AngleAxis(ctx.DetourSign * 65f, ctx.Up) * goal;
            }

            // Chaotic weave: sway left/right around the straight line, fading to 0 as it nears the
            // target so the mob still converges onto the module. Per-mob phase = unsynced wandering.
            ctx.WanderTime += dt;
            if (ctx.Config.WanderAngle > 0f)
            {
                float falloff = Mathf.Clamp01((dist - ctx.Config.LatchDistance) / 3f);
                if (falloff > 0f)
                {
                    float w = Mathf.Sin(ctx.WanderTime * ctx.Config.WanderSpeed + ctx.WanderSeed)
                              * ctx.Config.WanderAngle * falloff;
                    goal = Quaternion.AngleAxis(w, ctx.Up) * goal;
                }
            }

            ctx.Crawler.Step(ctx, goal, ctx.Config.CrawlSpeed, dt);

            if (dist < ctx.LastTargetDistance - ctx.Config.ProgressEpsilon)
            {
                ctx.LastTargetDistance = dist;
                ctx.StuckTimer = 0f;
            }
            else
            {
                ctx.StuckTimer += dt;
                if (ctx.StuckTimer > ctx.Config.StuckTime && ctx.DetourTimer <= 0f)
                {
                    ctx.StuckTimer = 0f;
                    ctx.LastTargetDistance = dist;
                    ctx.DetourTimer = 0.8f;
                    ctx.DetourSign = -ctx.DetourSign;
                }
            }

            if (ctx.PounceCooldownLeft <= 0f && dist <= ctx.Config.PounceRange && HasLineOfSight(ctx))
                return EnemyStateId.Pounce;
            return Id;
        }

        private static bool HasLineOfSight(EnemyContext ctx)
        {
            Vector3 from = ctx.Body.position + ctx.Up * 0.3f;
            Vector3 to = ctx.Target.Position;
            Vector3 dir = to - from;
            float d = dir.magnitude;
            if (d <= ctx.Config.LatchDistance) return true;
            return !Physics.Raycast(from, dir / d, d - ctx.Config.LatchDistance, ctx.Config.SurfaceMask, QueryTriggerInteraction.Ignore);
        }
    }
}
