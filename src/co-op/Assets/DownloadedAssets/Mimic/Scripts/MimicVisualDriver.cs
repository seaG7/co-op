using UnityEngine;

namespace MimicSpace
{
    [RequireComponent(typeof(Mimic))]
    public class MimicVisualDriver : MonoBehaviour
    {
        public float velocityLerpCoef = 6f;

        private Mimic _mimic;
        private Vector3 _lastPosition;
        private Vector3 _velocity;

        private void Awake()
        {
            _mimic = GetComponent<Mimic>();
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _velocity = Vector3.zero;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _mimic == null) return;

            Vector3 delta = transform.position - _lastPosition;
            delta.y = 0f;
            _lastPosition = transform.position;

            _velocity = Vector3.Lerp(_velocity, delta / dt, velocityLerpCoef * dt);
            _mimic.velocity = _velocity;
        }
    }
}
