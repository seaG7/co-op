using UnityEngine;
using Gameplay.World.Enemies.AI;

namespace CoOp.Tests.EditMode.Enemies
{
    public sealed class FakeSurfaceProbe : ISurfaceProbe
    {
        public delegate bool ProbeFn(Vector3 origin, Vector3 dir, float dist, out ProbeHit hit);
        public ProbeFn Handler;

        public static FakeSurfaceProbe FlatGround(float y)
        {
            var p = new FakeSurfaceProbe();
            p.Handler = (Vector3 o, Vector3 d, float dist, out ProbeHit hit) =>
            {
                hit = default;
                if (d.y >= -0.01f) return false;
                float t = (o.y - y) / -d.y;
                if (t < 0f || t > dist) return false;
                hit = new ProbeHit { Point = new Vector3(o.x + d.x * t, y, o.z + d.z * t), Normal = Vector3.up, Distance = t };
                return true;
            };
            return p;
        }

        public bool Raycast(Vector3 origin, Vector3 dir, float dist, out ProbeHit hit)
            => Handler(origin, dir.normalized, dist, out hit);
    }
}
