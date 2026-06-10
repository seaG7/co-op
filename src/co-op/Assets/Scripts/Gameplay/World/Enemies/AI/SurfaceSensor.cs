using UnityEngine;
using Data.Configs;

namespace Gameplay.World.Enemies.AI
{
    public sealed class SurfaceSensor
    {
        private readonly ISurfaceProbe _probe;
        public SurfaceSensor(ISurfaceProbe probe) { _probe = probe; }

        public bool TryStick(Vector3 pos, Vector3 up, EnemyConfig cfg, out ProbeHit hit)
            => _probe.Raycast(pos + up * cfg.StickProbeUp, -up, cfg.StickProbeUp + cfg.MaxStepDown, out hit);

        public WallInfo ForwardFan(Vector3 pos, Vector3 up, Vector3 heading, EnemyConfig cfg)
        {
            int n = Mathf.Max(1, cfg.FanRayCount);
            int hits = 0;
            Vector3 normalSum = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : (i / (float)(n - 1)) * 2f - 1f;
                Vector3 dir = (Quaternion.AngleAxis(t * cfg.FanHalfAngle, up) * heading).normalized;
                if (_probe.Raycast(pos + up * cfg.StepHeight, dir, cfg.LookAhead, out var h))
                {
                    if (Vector3.Angle(h.Normal, up) > cfg.WallAngleThreshold)
                    {
                        hits++;
                        normalSum += h.Normal;
                    }
                }
            }
            float coverage = (float)hits / n;
            return new WallInfo
            {
                Found = coverage >= cfg.ClimbCoverage,
                Normal = hits > 0 ? normalSum.normalized : up,
                Coverage = coverage
            };
        }

        public bool TryCornerSweep(Vector3 origin, Vector3 up, Vector3 heading, EnemyConfig cfg, out ProbeHit best)
        {
            best = default;
            float bestDist = float.MaxValue;
            bool found = false;
            Vector3 axis = Vector3.Cross(up, heading);
            if (axis.sqrMagnitude < 1e-6f) return false;
            axis.Normalize();
            float maxDist = cfg.StickProbeUp + cfg.MaxStepDown;
            int steps = Mathf.Max(1, cfg.SweepSteps);
            for (int i = 1; i <= steps; i++)
            {
                float ang = cfg.SweepMaxAngle * i / steps;
                Vector3 d1 = (Quaternion.AngleAxis(ang, axis) * (-up)).normalized;
                Vector3 d2 = (Quaternion.AngleAxis(-ang, axis) * (-up)).normalized;
                if (_probe.Raycast(origin, d1, maxDist, out var h1) && h1.Distance < bestDist) { best = h1; bestDist = h1.Distance; found = true; }
                if (_probe.Raycast(origin, d2, maxDist, out var h2) && h2.Distance < bestDist) { best = h2; bestDist = h2.Distance; found = true; }
                if (found) return true;
            }
            return found;
        }
    }
}
