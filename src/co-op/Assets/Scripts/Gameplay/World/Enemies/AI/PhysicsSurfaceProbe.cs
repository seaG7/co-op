using UnityEngine;

namespace Gameplay.World.Enemies.AI
{
    public sealed class PhysicsSurfaceProbe : ISurfaceProbe
    {
        private readonly int _mask;
        private readonly Transform _ignoreRoot;
        private readonly RaycastHit[] _buf = new RaycastHit[8];

        public PhysicsSurfaceProbe(LayerMask mask, Transform ignoreRoot)
        {
            _mask = mask;
            _ignoreRoot = ignoreRoot;
        }

        public bool Raycast(Vector3 origin, Vector3 dir, float dist, out ProbeHit hit)
        {
            hit = default;
            int count = Physics.RaycastNonAlloc(origin, dir, _buf, dist, _mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var h = _buf[i];
                if (_ignoreRoot != null && h.collider != null && h.collider.transform.IsChildOf(_ignoreRoot)) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    hit = new ProbeHit { Point = h.point, Normal = h.normal, Distance = h.distance };
                    found = true;
                }
            }
            return found;
        }
    }
}
