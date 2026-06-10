using UnityEngine;

namespace Infrastructure.Services.Enemies
{
    public static class TargetingMath
    {
        public static bool IsBlockingPlayer(Vector3 enemyPos, Vector3 playerPos, Vector3 cannonPos, float aggroRadius, float blockingAngleDeg)
        {
            Vector3 toPlayer = playerPos - enemyPos;
            float playerDist = toPlayer.magnitude;
            if (playerDist > aggroRadius || playerDist < 1e-3f) return false;

            Vector3 toCannon = cannonPos - enemyPos;
            float cannonDist = toCannon.magnitude;
            if (cannonDist > 1e-3f && playerDist > cannonDist) return false;

            if (cannonDist < 1e-3f) return true;
            float angle = Vector3.Angle(toPlayer / playerDist, toCannon / cannonDist);
            return angle <= blockingAngleDeg;
        }
    }
}
