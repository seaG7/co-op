using UnityEngine;

namespace Gameplay.Player.Carry
{

    public static class CarrySolver
    {

        public static CarryTarget SolveTarget(HolderGrip[] holders, Vector3[] anchorsLocal, Vector3 upHint)
        {
            if (holders == null || holders.Length == 0 || anchorsLocal == null || anchorsLocal.Length < holders.Length)
                return new CarryTarget(Vector3.zero, Quaternion.identity);

            if (holders.Length == 1)
            {
                var up = holders[0].Up.sqrMagnitude > 1e-6f ? holders[0].Up : upHint;
                var fwd = holders[0].Forward.sqrMagnitude > 1e-6f ? holders[0].Forward : Vector3.forward;
                var rot = Quaternion.LookRotation(fwd, up);
                var pos = holders[0].GripPoint - rot * anchorsLocal[0];
                return new CarryTarget(pos, rot);
            }

            Vector3 gripAxis = holders[1].GripPoint - holders[0].GripPoint;
            Vector3 anchorAxis = anchorsLocal[1] - anchorsLocal[0];
            Vector3 up2 = holders[0].Up + holders[1].Up;
            if (up2.sqrMagnitude < 1e-6f) up2 = upHint;

            Quaternion rot2;
            if (gripAxis.sqrMagnitude < 1e-6f || anchorAxis.sqrMagnitude < 1e-6f)
            {
                Vector3 avgFwd = holders[0].Forward + holders[1].Forward;
                if (avgFwd.sqrMagnitude < 1e-6f) avgFwd = Vector3.forward;
                rot2 = Quaternion.LookRotation(avgFwd.normalized, up2.normalized);
            }
            else
            {

                Quaternion axisRot = Quaternion.FromToRotation(anchorAxis.normalized, gripAxis.normalized);

                Vector3 itemUp = axisRot * Vector3.up;
                Vector3 desiredUp = Vector3.ProjectOnPlane(up2, gripAxis).normalized;
                Vector3 itemUpOnPlane = Vector3.ProjectOnPlane(itemUp, gripAxis).normalized;
                Quaternion rollFix = (desiredUp.sqrMagnitude < 1e-6f || itemUpOnPlane.sqrMagnitude < 1e-6f)
                    ? Quaternion.identity
                    : Quaternion.FromToRotation(itemUpOnPlane, desiredUp);
                rot2 = rollFix * axisRot;
            }

            Vector3 midGrip = (holders[0].GripPoint + holders[1].GripPoint) * 0.5f;
            Vector3 midAnchor = (anchorsLocal[0] + anchorsLocal[1]) * 0.5f;
            Vector3 pos2 = midGrip - rot2 * midAnchor;
            return new CarryTarget(pos2, rot2);
        }

        public static Vector3 FollowVelocity(Vector3 current, Vector3 target, float dt, float maxSpeed, float responsiveness)
        {
            if (dt <= 0f) return Vector3.zero;
            Vector3 toTarget = target - current;
            Vector3 v = toTarget * responsiveness;
            return Vector3.ClampMagnitude(v, Mathf.Max(0f, maxSpeed));
        }

        public static Vector3 AngularVelocity(Quaternion current, Quaternion target, float dt, float maxDegPerSec)
        {
            if (dt <= 0f) return Vector3.zero;
            Quaternion delta = target * Quaternion.Inverse(current);
            delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (axis.sqrMagnitude < 1e-6f || float.IsInfinity(axis.x) || float.IsNaN(axis.x)) return Vector3.zero;
            if (angleDeg > 180f) angleDeg -= 360f;
            float speed = Mathf.Clamp(angleDeg, -maxDegPerSec, maxDegPerSec);
            return axis.normalized * (speed * Mathf.Deg2Rad);
        }
    }
}
