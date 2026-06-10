using NUnit.Framework;
using UnityEngine;
using Data.Configs;
using Gameplay.World.Enemies.AI;

namespace CoOp.Tests.EditMode.Enemies
{
    public class SurfaceCrawlerTests
    {
        private static EnemyContext MakeCtx(ISurfaceProbe probe, out GameObject go)
        {
            go = new GameObject("body");
            var cfg = ScriptableObject.CreateInstance<EnemyConfig>();
            var sensor = new SurfaceSensor(probe);
            return new EnemyContext { Body = go.transform, Config = cfg, Crawler = new SurfaceCrawler(sensor), Up = Vector3.up, Forward = Vector3.forward };
        }

        [Test]
        public void FlatGround_MovesTowardGoal_StaysOnSurface()
        {
            var ctx = MakeCtx(FakeSurfaceProbe.FlatGround(0f), out var go);
            ctx.Body.position = new Vector3(0f, 0.15f, 0f);
            ctx.Crawler.Step(ctx, Vector3.forward, 3f, 0.1f);
            Assert.That(ctx.Body.position.z, Is.GreaterThan(0.1f));
            Assert.That(ctx.Body.position.y, Is.EqualTo(0.15f).Within(0.05f));
            Assert.That(Vector3.Dot(ctx.Up, Vector3.up), Is.GreaterThan(0.99f));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void WallAhead_AlignsUpTowardWallNormal()
        {
            var probe = new FakeSurfaceProbe();
            probe.Handler = (Vector3 o, Vector3 d, float dist, out ProbeHit hit) =>
            {
                hit = default;
                if (Vector3.Dot(d, Vector3.forward) > 0.3f) { hit = new ProbeHit { Point = new Vector3(0f, o.y, 1f), Normal = Vector3.back, Distance = 0.3f }; return true; }
                if (d.y < -0.5f) { hit = new ProbeHit { Point = new Vector3(o.x, 0f, o.z), Normal = Vector3.up, Distance = o.y }; return true; }
                return false;
            };
            var ctx = MakeCtx(probe, out var go);
            ctx.Body.position = new Vector3(0f, 0.15f, 0.5f);
            for (int i = 0; i < 20; i++) ctx.Crawler.Step(ctx, Vector3.forward, 3f, 0.05f);
            Assert.That(Vector3.Dot(ctx.Up, Vector3.back), Is.GreaterThan(0.3f), "up should rotate toward the wall normal as it climbs");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void NoSurface_GoesAirborne()
        {
            var probe = new FakeSurfaceProbe { Handler = (Vector3 o, Vector3 d, float dist, out ProbeHit hit) => { hit = default; return false; } };
            var ctx = MakeCtx(probe, out var go);
            ctx.Body.position = new Vector3(0f, 5f, 0f);
            ctx.Crawler.Step(ctx, Vector3.forward, 3f, 0.1f);
            Assert.IsTrue(ctx.Airborne);
            Assert.That(ctx.Body.position.y, Is.LessThan(5f));
            Object.DestroyImmediate(go);
        }
    }
}
