using NUnit.Framework;
using UnityEngine;
using Data.Configs;
using Gameplay.World.Enemies.AI;

namespace CoOp.Tests.EditMode.Enemies
{
    public class SurfaceSensorTests
    {
        private static EnemyConfig Cfg() => ScriptableObject.CreateInstance<EnemyConfig>();

        [Test]
        public void Stick_OnFlatGround_HitsBelow()
        {
            var sensor = new SurfaceSensor(FakeSurfaceProbe.FlatGround(0f));
            bool ok = sensor.TryStick(new Vector3(0f, 0.15f, 0f), Vector3.up, Cfg(), out var hit);
            Assert.IsTrue(ok);
            Assert.AreEqual(0f, hit.Point.y, 1e-3f);
            Assert.That(Vector3.Dot(hit.Normal, Vector3.up), Is.GreaterThan(0.99f));
        }

        [Test]
        public void ForwardFan_OpenGround_NoWall()
        {
            var sensor = new SurfaceSensor(FakeSurfaceProbe.FlatGround(0f));
            var wall = sensor.ForwardFan(Vector3.zero, Vector3.up, Vector3.forward, Cfg());
            Assert.IsFalse(wall.Found);
        }

        [Test]
        public void ForwardFan_WallAhead_DetectedAsClimb()
        {
            var probe = new FakeSurfaceProbe();
            probe.Handler = (Vector3 o, Vector3 d, float dist, out ProbeHit hit) =>
            {
                hit = default;
                if (Vector3.Dot(d, Vector3.forward) > 0.3f)
                { hit = new ProbeHit { Point = o + d * 0.3f, Normal = Vector3.back, Distance = 0.3f }; return true; }
                return false;
            };
            var sensor = new SurfaceSensor(probe);
            var wall = sensor.ForwardFan(Vector3.zero, Vector3.up, Vector3.forward, Cfg());
            Assert.IsTrue(wall.Found);
            Assert.That(Vector3.Dot(wall.Normal, Vector3.back), Is.GreaterThan(0.9f));
        }
    }
}
