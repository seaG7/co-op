using System;
using UnityEngine;

namespace Gameplay.World.Weapon
{
    public sealed class Harpoon : MonoBehaviour
    {
        public enum State { Docked, Flying, Landed, Returning }

        [Header("Parts")]
        [Tooltip("The object that flies — the whole harpoon body/root. If null, this component's own transform. Its authored local pose (resting in the barrel) IS the dock — no separate dock point needed.")]
        [SerializeField] private Transform _body;
        [Tooltip("The harpoon tip (child marker at the pointy end). The nose lands exactly on the aim point, and the shot launches from here.")]
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

        private Transform Body => _body != null ? _body : transform;
        public Vector3 NoseWorldPosition => _nose != null ? _nose.position : Body.position;

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

        private Vector3 _dockLocalPos;
        private Quaternion _dockLocalRot;
        private bool _dockCaptured;

        private Vector3 DockWorldPos => Body.parent != null ? Body.parent.TransformPoint(_dockLocalPos) : _dockLocalPos;
        private Quaternion DockWorldRot => Body.parent != null ? Body.parent.rotation * _dockLocalRot : _dockLocalRot;

        private void Awake()
        {
            if (Body.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
            CaptureDock();
            _noseOffset = _nose != null ? Vector3.Distance(Body.position, _nose.position) : 0f;
        }

        private void OnEnable()
        {
            Current = State.Docked;
            SnapToDock();
        }

        private void CaptureDock()
        {
            if (_dockCaptured) return;
            _dockLocalPos = Body.localPosition;
            _dockLocalRot = Body.localRotation;
            _dockCaptured = true;
        }

        public float EstimateFlightSeconds(Vector3 origin, Vector3 target)
            => Mathf.Clamp(Vector3.Distance(origin, target) / Mathf.Max(1f, _launchSpeed), _minFlightTime, _maxFlightTime);

        public float EstimateCycleSeconds(Vector3 origin, Vector3 target)
        {
            float flight = EstimateFlightSeconds(origin, target);
            float ret = Mathf.Clamp(Vector3.Distance(target, DockWorldPos) / Mathf.Max(1f, _returnSpeed), _minReturnTime, _maxReturnTime);
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
            if (!_dockCaptured) CaptureDock();
            Body.localPosition = _dockLocalPos;
            Body.localRotation = _dockLocalRot;
        }

        private void TickFlying(float dt)
        {
            _t += dt;
            float t = Mathf.Min(_t, _flightTime);
            Vector3 nose = _origin + _v0 * t + 0.5f * _grav * (t * t);
            Vector3 vel = _v0 + _grav * t;
            Quaternion rot = vel.sqrMagnitude > 1e-5f ? Quaternion.LookRotation(vel.normalized, Vector3.up) : Body.rotation;
            if (_flightSpin != 0f)
            {
                _roll += _flightSpin * dt;
                rot *= Quaternion.AngleAxis(_roll, Vector3.forward);
            }
            Body.SetPositionAndRotation(nose - (rot * Vector3.forward) * _noseOffset, rot);

            if (_t < _flightTime) return;

            Vector3 finalVel = _v0 + _grav * _flightTime;
            Quaternion finalRot = finalVel.sqrMagnitude > 1e-5f ? Quaternion.LookRotation(finalVel.normalized, Vector3.up) : Body.rotation;
            Body.SetPositionAndRotation(_target - (finalRot * Vector3.forward) * _noseOffset, finalRot);
            Current = State.Landed;
            _landedTimer = 0f;
            Landed?.Invoke(_target);
        }

        private void TickLanded(float dt)
        {
            _landedTimer += dt;
            if (_landedTimer < Mathf.Max(0f, _landedWait)) return;
            _returnFromPos = Body.position;
            _returnFromRot = Body.rotation;
            _returnDur = Mathf.Clamp(Vector3.Distance(Body.position, DockWorldPos) / Mathf.Max(1f, _returnSpeed), _minReturnTime, _maxReturnTime);
            _returnT = 0f;
            Current = State.Returning;
        }

        private void TickReturning(float dt)
        {
            _returnT += dt;
            float k = _returnDur > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_returnT / _returnDur)) : 1f;
            Body.SetPositionAndRotation(
                Vector3.Lerp(_returnFromPos, DockWorldPos, k),
                Quaternion.Slerp(_returnFromRot, DockWorldRot, k));
            if (k < 1f) return;
            Current = State.Docked;
            Redocked?.Invoke();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 1f);
            Gizmos.DrawWireSphere(Body.position, 0.08f);
            UnityEditor.Handles.Label(Body.position, "Harpoon rest (dock)");
            if (_nose != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.3f, 1f);
                Gizmos.DrawWireSphere(_nose.position, 0.06f);
                Gizmos.DrawLine(Body.position, _nose.position);
                UnityEditor.Handles.Label(_nose.position, "Nose / launch point");
            }
        }
#endif
    }
}
