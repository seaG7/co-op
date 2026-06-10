using NUnit.Framework;
using UnityEngine;
using Infrastructure.Services.Enemies;

namespace CoOp.Tests.EditMode.Enemies
{
    public class TargetingMathTests
    {
        [Test]
        public void PlayerBetweenEnemyAndCannon_WithinAggro_IsBlocking()
        {
            bool blocking = TargetingMath.IsBlockingPlayer(
                Vector3.zero, new Vector3(0f, 0f, 3f), new Vector3(0f, 0f, 10f), 6f, 45f);
            Assert.IsTrue(blocking);
        }

        [Test]
        public void PlayerOutsideAggro_NotBlocking()
        {
            bool blocking = TargetingMath.IsBlockingPlayer(Vector3.zero, new Vector3(0f, 0f, 9f), new Vector3(0f, 0f, 10f), 6f, 45f);
            Assert.IsFalse(blocking);
        }

        [Test]
        public void PlayerOffToTheSide_NotBlocking()
        {
            bool blocking = TargetingMath.IsBlockingPlayer(Vector3.zero, new Vector3(5f, 0f, 0.5f), new Vector3(0f, 0f, 10f), 6f, 45f);
            Assert.IsFalse(blocking);
        }

        [Test]
        public void PlayerFartherThanCannon_NotBlocking()
        {
            bool blocking = TargetingMath.IsBlockingPlayer(Vector3.zero, new Vector3(0f, 0f, 12f), new Vector3(0f, 0f, 10f), 20f, 45f);
            Assert.IsFalse(blocking);
        }
    }
}
