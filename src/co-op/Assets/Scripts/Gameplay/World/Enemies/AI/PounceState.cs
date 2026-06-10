using UnityEngine;

namespace Gameplay.World.Enemies.AI
{
    public sealed class PounceState : IEnemyState
    {
        public EnemyStateId Id => EnemyStateId.Pounce;
        private Vector3 _start;
        private float _t, _dur;
        private bool _announced;

        public void Enter(EnemyContext ctx)
        {
            _start = ctx.Body.position;
            float dist = Vector3.Distance(_start, ctx.Target.Position);
            _dur = Mathf.Max(0.05f, dist / Mathf.Max(0.1f, ctx.Config.PounceSpeed));
            _t = 0f;
            _announced = false;
            ctx.PendingEffect = EnemyEffectKind.PrePounce;
        }

        public void Exit(EnemyContext ctx) { ctx.PounceCooldownLeft = ctx.Config.PounceCooldown; }

        public EnemyStateId Tick(EnemyContext ctx, float dt)
        {
            if (ctx.DeadRequested) return EnemyStateId.Dead;
            if (!ctx.Target.IsValid) return EnemyStateId.Pursue;

            _t += dt;
            if (!_announced) { _announced = true; ctx.PendingEffect = EnemyEffectKind.Pounced; }
            float u = Mathf.Clamp01(_t / _dur);
            Vector3 target = ctx.Target.Position;
            Vector3 p = Vector3.Lerp(_start, target, u) + ctx.Up * (Mathf.Sin(u * Mathf.PI) * ctx.Config.PounceArcHeight);

            Vector3 face = Vector3.ProjectOnPlane(target - ctx.Body.position, ctx.Up);
            if (face.sqrMagnitude > 1e-4f)
                ctx.Body.rotation = Quaternion.LookRotation(face.normalized, ctx.Up);
            ctx.Body.position = p;

            if (Vector3.Distance(ctx.Body.position, target) <= ctx.Config.LatchDistance) return EnemyStateId.Latched;
            if (_t >= ctx.Config.PounceTimeout) return EnemyStateId.Pursue;
            return Id;
        }
    }
}
