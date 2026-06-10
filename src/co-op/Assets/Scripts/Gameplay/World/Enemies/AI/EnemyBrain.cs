using UnityEngine;
using Data.Configs;
using Infrastructure.Services.Enemies;

namespace Gameplay.World.Enemies.AI
{
    public sealed class EnemyBrain
    {
        private readonly EnemyContext _ctx;
        private readonly EnemyStateMachine _fsm;

        public EnemyContext Context => _ctx;
        public EnemyStateId StateId => _fsm.CurrentId;

        public EnemyBrain(Transform body, EnemyConfig cfg, IEnemyTargetingService targeting)
        {
            var probe = new PhysicsSurfaceProbe(cfg.SurfaceMask, body);
            var sensor = new SurfaceSensor(probe);
            _ctx = new EnemyContext
            {
                Body = body,
                Config = cfg,
                Crawler = new SurfaceCrawler(sensor),
                Targeting = targeting,
                Up = body.up,
                Forward = body.forward
            };
            _fsm = new EnemyStateMachine(_ctx, new IEnemyState[]
            {
                new PursueState(), new PounceState(), new LatchedState(), new DeadState()
            }, EnemyStateId.Pursue);
        }

        public void Tick(float dt) => _fsm.Tick(dt);

        public void RequestDead()
        {
            _ctx.DeadRequested = true;
            _fsm.Force(EnemyStateId.Dead);
        }
    }
}
