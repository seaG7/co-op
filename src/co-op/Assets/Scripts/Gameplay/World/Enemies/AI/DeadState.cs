namespace Gameplay.World.Enemies.AI
{
    public sealed class DeadState : IEnemyState
    {
        public EnemyStateId Id => EnemyStateId.Dead;
        public void Enter(EnemyContext ctx) { }
        public void Exit(EnemyContext ctx) { }
        public EnemyStateId Tick(EnemyContext ctx, float dt) => Id;
    }
}
