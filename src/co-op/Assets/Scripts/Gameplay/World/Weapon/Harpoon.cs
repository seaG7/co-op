using System;
using UnityEngine;

namespace Gameplay.World.Weapon
{
    public sealed class Harpoon : MonoBehaviour
    {
        public enum State { Docked, Flying, Landed, Returning }

        [Header("Anchors")]
        [Tooltip("Resting pose at the muzzle. The harpoon snaps here while docked (follows the turret) and is reeled back here.")]
        [SerializeField] private Transform _dock;
        [Tooltip("Optional nose marker. The distance root→nose is used so the NOSE lands exactly on the aim point. Null = the prefab pivot is the nose.")]
        [SerializeField] private Transform _nose;

        [Header("Flight")]
        [Tooltip("Nominal launch speed (m/s). Flight time = distance / this, clamped.")]
        [SerializeField] private float _launchSpeed = 45f;
        [SerializeField] private float _minFlightTime = 0.12f;
        [SerializeField] private float _maxFlightTime = 0.8f;
        [Tooltip("Downward accel during flight (m/s²). Small = subtle arc; launch is solved so the nose still lands on the point.")]
        [SerializeField] private float _gravity = 8f;
        [Tooltip("Degrees/sec the harpoon rolls around its forward axis while flying. 0 = none.")]
        [SerializeField] private float _flightSpin = 0f;

        [Header("Return")]
        [Tooltip("Seconds the harpoon sticks in the surface before being reeled back.")]
        [SerializeField] private float _landedWait = 1f;
        [Tooltip("Reel-in speed (m/s) for the straight return path. Return time = distance / this, clamped.")]
        [SerializeField] private float _returnSpeed = 30f;
        [SerializeField] private float _minReturnTime = 0.15f;
        [SerializeField] private float _maxReturnTime = 0.7f;

        public event Action<Vector3> Landed;
        public event Action Redocked;

        public State Current { get; private set; } = State.Docked;
        public bool IsDocked => Current == State.Docked;

        private float _noseOffset;
        private Vector3 _origin;
        private Vector3 _target;
        private Vector3 _v0;
        private Vector3 _grav;
        private float _flightTime;
        private float _t;
        private float _landedTimer;
        private Vector3 _returnFromPos;
        private Quaternion _returnFromRot;
        private float _returnT;
        private float _returnDur;
        private float _roll;

        private void Awake()
        {
            if (TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
            _noseOffset = _nose != null ? Vector3.Distance(transform.position, _nose.position) : 0f;
        }

        private void OnEnable()
        {
            Current = State.Docked;
            SnapToDock();
        }

        public float EstimateFlightSeconds(Vector3 origin, Vector3 target)
            => Mathf.Clamp(Vector3.Distance(origin, target) / Mathf.Max(1f, _launchSpeed), _minFlightTime, _maxFlightTime);

        public float EstimateCycleSeconds(Vector3 origin, Vector3 target)
        {
            float flight = EstimateFlightSeconds(origin, target);
            float dockDist = _dock != null ? Vector3.Distance(target, _dock.position) : Vector3.Distance(target, origin);
            float ret = Mathf.Clamp(dockDist / Mathf.Max(1f, _returnSpeed), _minReturnTime, _maxReturnTime);
            return flight + Mathf.Max(0f, _landedWait) + ret + 0.1f;
        }

        public void Launch(Vector3 origin, Vector3 target)
        {
            _origin = origin;
            _target = target;
            _grav = Vector3.down * Mathf.Max(0f, _gravity);
            _flightTime = EstimateFlightSeconds(origin, target);
            _v0 = (target - origin) / _flightTime - 0.5f * _grav * _flightTime;
            _t = 0f;
            _roll = 0f;
            Current = State.Flying;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (Current)
            {
                case State.Docked: SnapToDock(); break;
                case State.Flying: TickFlying(dt); break;
                case State.Landed: TickLanded(dt); break;
                case State.Returning: TickReturning(dt); break;
            }
        }

        private void SnapToDock()
        {
            if (_dock != null) transform.SetPositionAndRotation(_dock.position, _dock.rotation);
        }

        private void TickFlying(float dt)
        {
            _t += dt;
            float t = Mathf.Min(_t, _flightTime);
            Vector3 nose = _origin + _v0 * t + 0.5f * _grav * (t * t);
            Vector3 vel = _v0 + _grav * t;
            Quaternion rot = vel.sqrMagnitude > 1e-5f ? Quaternion.LookRotation(vel.normalized, Vector3.up) : transform.rotation;
            if (_flightSpin != 0f)
            {
                _roll += _flightSpin * dt;
                rot *= Quaternion.AngleAxis(_roll, Vector3.forward);
            }
            transform.SetPositionAndRotation(nose - (rot * Vector3.forward) * _noseOffset, rot);

            if (_t < _flightTime) return;

            Vector3 finalVel = _v0 + _grav * _flightTime;
            Quaternion finalRot = finalVel.sqrMagnitude > 1e-5f ? Quaternion.LookRotation(finalVel.normalized, Vector3.up) : transform.rotation;
            transform.SetPositionAndRotation(_target - (finalRot * Vector3.forward) * _noseOffset, finalRot);
            Current = State.Landed;
            _landedTimer = 0f;
            Landed?.Invoke(_target);
        }

        private void TickLanded(float dt)
        {
            _landedTimer += dt;
            if (_landedTimer < Mathf.Max(0f, _landedWait)) return;
            _returnFromPos = transform.position;
            _returnFromRot = transform.rotation;
            float dist = _dock != null ? Vector3.Distance(transform.position, _dock.position) : 0f;
            _returnDur = Mathf.Clamp(dist / Mathf.Max(1f, _returnSpeed), _minReturnTime, _maxReturnTime);
            _returnT = 0f;
            Current = State.Returning;
        }

        private void TickReturning(float dt)
        {
            if (_dock == null)
            {
                Current = State.Docked;
                Redocked?.Invoke();
                return;
            }
            _returnT += dt;
            float k = _returnDur > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_returnT / _returnDur)) : 1f;
            transform.SetPositionAndRotation(
                Vector3.Lerp(_returnFromPos, _dock.position, k),
                Quaternion.Slerp(_returnFromRot, _dock.rotation, k));
            if (k < 1f) return;
            Current = State.Docked;
            Redocked?.Invoke();
        }
    }
}
