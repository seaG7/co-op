using NUnit.Framework;
using Gameplay.World.Enemies.AI;

namespace CoOp.Tests.EditMode.Enemies
{
    public class EnemyStateMachineTests
    {
        private sealed class StubState : IEnemyState
        {
            public EnemyStateId Id { get; set; }
            public EnemyStateId Next;
            public int Entered, Exited;
            public StubState(EnemyStateId id) { Id = id; Next = id; }
            public void Enter(EnemyContext ctx) => Entered++;
            public void Exit(EnemyContext ctx) => Exited++;
            public EnemyStateId Tick(EnemyContext ctx, float dt) => Next;
        }

        [Test]
        public void Transitions_OnReturnedId_CallsExitThenEnter()
        {
            var a = new StubState(EnemyStateId.Pursue) { Next = EnemyStateId.Pounce };
            var b = new StubState(EnemyStateId.Pounce);
            var fsm = new EnemyStateMachine(new EnemyContext(), new IEnemyState[] { a, b }, EnemyStateId.Pursue);
            Assert.AreEqual(1, a.Entered);
            fsm.Tick(0.1f);
            Assert.AreEqual(EnemyStateId.Pounce, fsm.CurrentId);
            Assert.AreEqual(1, a.Exited);
            Assert.AreEqual(1, b.Entered);
        }

        [Test]
        public void StaysInState_WhenTickReturnsSameId()
        {
            var a = new StubState(EnemyStateId.Pursue);
            var fsm = new EnemyStateMachine(new EnemyContext(), new IEnemyState[] { a }, EnemyStateId.Pursue);
            fsm.Tick(0.1f);
            Assert.AreEqual(EnemyStateId.Pursue, fsm.CurrentId);
            Assert.AreEqual(0, a.Exited);
        }
    }
}
