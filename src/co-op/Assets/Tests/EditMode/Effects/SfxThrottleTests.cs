using System.Collections.Generic;
using NUnit.Framework;
using Data.Effects;
using Infrastructure.Services.Effects;

namespace CoOp.Tests.EditMode.Effects
{
    public class SfxThrottleTests
    {
        [Test]
        public void NoThrottle_AlwaysPasses()
        {
            var last = new Dictionary<SfxId, float>();
            Assert.IsTrue(SfxService.PassesThrottle(last, SfxId.EnemyStep, 0f, 0f));
            Assert.IsTrue(SfxService.PassesThrottle(last, SfxId.EnemyStep, 0f, 0.01f));
        }

        [Test]
        public void Throttle_BlocksWithinInterval_AllowsAfter()
        {
            var last = new Dictionary<SfxId, float>();
            Assert.IsTrue(SfxService.PassesThrottle(last, SfxId.EnemyStep, 0.2f, 1.0f));
            Assert.IsFalse(SfxService.PassesThrottle(last, SfxId.EnemyStep, 0.2f, 1.1f));
            Assert.IsTrue(SfxService.PassesThrottle(last, SfxId.EnemyStep, 0.2f, 1.25f));
        }
    }
}
