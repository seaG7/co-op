using UnityEngine;

namespace Gameplay.World.Weapon
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class HarpoonRope : MonoBehaviour
    {
        [Header("Endpoints")]
        [Tooltip("Rope start — the reel on the cannon.")]
        [SerializeField] private Transform _reel;
        [Tooltip("Rope end — a point on the harpoon (e.g. its tail).")]
        [SerializeField] private Transform _harpoon;

        [Header("Rope")]
        [SerializeField, Min(2)] private int _segments = 20;
        [Tooltip("Extra length over the straight reel→harpoon distance (fraction). Higher = more sag.")]
        [SerializeField, Min(0f)] private float _slack = 0.08f;
        [Tooltip("Downward accel pulling the rope into a sag (m/s²).")]
        [SerializeField] private float _gravity = 9f;
        [Range(0f, 1f)]
        [Tooltip("Velocity damping per step. Higher = stiffer, less swing.")]
        [SerializeField] private float _damping = 0.1f;
        [SerializeField, Min(1)] private int _iterations = 12;

        private LineRenderer _line;
        private Vector3[] _pos;
        private Vector3[] _prev;
        private bool _seeded;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            int n = Mathf.Max(2, _segments);
            _line.positionCount = n;
            _pos = new Vector3[n];
            _prev = new Vector3[n];
        }

        private void OnEnable() => _seeded = false;

        private void LateUpdate()
        {
            if (_line == null || _reel == null || _harpoon == null) return;

            Vector3 a = _reel.position;
            Vector3 b = _harpoon.position;
            int n = _pos.Length;

            if (!_seeded)
            {
                for (int i = 0; i < n; i++)
                {
                    float f = i / (float)(n - 1);
                    _pos[i] = _prev[i] = Vector3.Lerp(a, b, f);
                }
                _seeded = true;
            }

            float dt = Time.deltaTime;
            Vector3 gravityStep = Vector3.down * _gravity * dt * dt;
            float keep = 1f - Mathf.Clamp01(_damping);

            for (int i = 1; i < n - 1; i++)
            {
                Vector3 cur = _pos[i];
                Vector3 vel = (cur - _prev[i]) * keep;
                _prev[i] = cur;
                _pos[i] = cur + vel + gravityStep;
            }

            _pos[0] = a;
            _pos[n - 1] = b;

            float restTotal = Vector3.Distance(a, b) * (1f + _slack);
            float seg = restTotal / (n - 1);

            for (int it = 0; it < _iterations; it++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 delta = _pos[i + 1] - _pos[i];
                    float dist = delta.magnitude;
                    if (dist < 1e-5f) continue;
                    Vector3 corr = delta * (0.5f * (dist - seg) / dist);
                    if (i != 0) _pos[i] += corr;
                    if (i + 1 != n - 1) _pos[i + 1] -= corr;
                }
                _pos[0] = a;
                _pos[n - 1] = b;
            }

            _line.SetPositions(_pos);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_reel == null || _harpoon == null) return;
            Vector3 a = _reel.position, b = _harpoon.position;
            float dist = Vector3.Distance(a, b);
            int n = Mathf.Max(2, _segments);
            Gizmos.color = new Color(0.95f, 0.85f, 0.4f, 0.95f);
            Vector3 prev = a;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * _slack * dist;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 1f);
            Gizmos.DrawWireSphere(a, 0.05f);
            Gizmos.DrawWireSphere(b, 0.05f);
        }
#endif
    }
}
