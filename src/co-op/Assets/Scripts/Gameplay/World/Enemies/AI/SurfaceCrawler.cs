using UnityEngine;

namespace Gameplay.World.Enemies.AI
{
    public sealed class SurfaceCrawler
    {
        private readonly SurfaceSensor _sensor;
        public SurfaceCrawler(SurfaceSensor sensor) { _sensor = sensor; }

        public void Step(EnemyContext ctx, Vector3 worldGoalDir, float speed, float dt)
        {
            var cfg = ctx.Config;
            Vector3 pos = ctx.Body.position;
            Vector3 up = ctx.Up;

            Vector3 tangent = Vector3.ProjectOnPlane(worldGoalDir, up);
            tangent = tangent.sqrMagnitude < 1e-6f ? ctx.Forward : tangent.normalized;

            WallInfo wall = _sensor.ForwardFan(pos, up, tangent, cfg);
            if (wall.Found)
            {
                up = Vector3.Slerp(up, wall.Normal, cfg.AlignRate * dt).normalized;
                tangent = Vector3.ProjectOnPlane(tangent, wall.Normal);
                tangent = tangent.sqrMagnitude < 1e-6f ? ctx.Forward : tangent.normalized;
            }

            Vector3 next = pos + tangent * (speed * dt);
            bool airborne = false;

            if (_sensor.TryStick(next, up, cfg, out var stick))
            {
                pos = stick.Point + stick.Normal * cfg.HoverHeight;
                up = Vector3.Slerp(up, stick.Normal, cfg.AlignRate * dt).normalized;
            }
            else if (_sensor.TryCornerSweep(next, up, tangent, cfg, out var wrap))
            {
                pos = wrap.Point + wrap.Normal * cfg.HoverHeight;
                up = wrap.Normal;
            }
            else
            {
                airborne = true;
                Vector3 falling = next + Vector3.down * (cfg.FallSpeed * dt);
                if (_sensor.TryStick(falling, Vector3.up, cfg, out var land))
                {
                    pos = land.Point + land.Normal * cfg.HoverHeight;
                    up = Vector3.Slerp(up, land.Normal, cfg.AlignRate * dt).normalized;
                    airborne = false;
                }
                else pos = falling;
            }

            Vector3 fwd = Vector3.ProjectOnPlane(tangent, up);
            fwd = fwd.sqrMagnitude < 1e-6f ? ctx.Forward : fwd.normalized;

            ctx.Up = up;
            ctx.Forward = fwd;
            ctx.Airborne = airborne;
            ctx.Body.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, up));
        }
    }
}
