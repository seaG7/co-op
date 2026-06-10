using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Gameplay.World.Enemies;
using Gameplay.World.Sources;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Enemies;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Weapon
{
    public sealed class Weapon : InjectableNetworkBehaviour
    {
        [SerializeField, HideInInspector] private List<WeaponModuleSlot> _slots = new();

        [Header("Turret")]
        [SerializeField] private Transform _turretPivot;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _cameraAnchor;

        [Header("Harpoon")]
        [SerializeField] private Harpoon _harpoon;
        [Tooltip("Camera pivot used for aiming — the aim ray goes from here through the crosshair point. Falls back to the camera anchor.")]
        [SerializeField] private Transform _cameraPivot;
        [Tooltip("3D crosshair/reticle point in front of the camera. Aim direction = (crosshairPoint - cameraPivot).")]
        [SerializeField] private Transform _crosshairPoint;
        [Tooltip("Layers the aim/fire ray may hit. EXCLUDE the harpoon's own layer (and ideally the operator) so the shot never lands on the docked harpoon.")]
        [SerializeField] private LayerMask _aimMask = ~0;

        [Inject] private IConfigDataProvider _configs;
        [Inject] private SignalBus _signalBus;
        [Inject] private IEnemyTargetingService _targeting;

        public readonly SyncVar<int> OperatorClientId = new(-1);
        public readonly SyncVar<int> CorpsesLoaded = new(0);
        private readonly SyncVar<Quaternion> _aimRot = new(Quaternion.identity);

        private float _desiredYaw;
        private float _desiredPitch;
        private Quaternion _localAim = Quaternion.identity;
        private Quaternion _lastSentAim = Quaternion.identity;

        private float _harpoonBusyUntil;
        private Source _pendingSource;
        private Enemy _pendingEnemy;
        private float _pendingDamage;
        private float _pendingApplyAt;
        private bool _pendingActive;

        public Transform CameraAnchor =>
            _cameraAnchor != null ? _cameraAnchor : (_turretPivot != null ? _turretPivot : transform);

        public bool IsFree => OperatorClientId.Value == -1;

        public bool IsLocalOperator =>
            base.IsClientInitialized && OperatorClientId.Value != -1
            && base.LocalConnection != null && OperatorClientId.Value == base.LocalConnection.ClientId;

        public int RequiredCorpses => _configs?.Weapon != null ? Mathf.Max(0, _configs.Weapon.RequiredCorpses) : 3;
        public bool IsCharged => CorpsesLoaded.Value >= RequiredCorpses;

        public void AddCorpse()
        {
            if (IsServerInitialized) CorpsesLoaded.Value++;
        }

        public void ServerDebugCharge() { if (IsServerInitialized) CorpsesLoaded.Value = RequiredCorpses; }

        public void ServerDebugAssemble()
        {
            if (!IsServerInitialized) return;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null) _slots[i].IsOccupied.Value = true;
        }

        public bool IsAssembled
        {
            get
            {
                if (_slots.Count == 0) return false;
                for (int i = 0; i < _slots.Count; i++)
                    if (_slots[i] == null || _slots[i].IsFree) return false;
                return true;
            }
        }

        private void Awake()
        {
            _slots.Clear();
            _slots.AddRange(GetComponentsInChildren<WeaponModuleSlot>(includeInactive: true));
            if (_turretPivot == null) _turretPivot = transform.Find("Turret");
            var pivot = _turretPivot != null ? _turretPivot : transform;
            if (_muzzle == null) _muzzle = pivot.Find("Muzzle");
            if (_cameraAnchor == null) _cameraAnchor = pivot.Find("CameraAnchor");
            if (_harpoon == null) _harpoon = GetComponentInChildren<Harpoon>(includeInactive: true);
            if (_cameraPivot == null) _cameraPivot = _cameraAnchor;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            CorpsesLoaded.OnChange += OnCorpsesChanged;
            if (_harpoon != null) _harpoon.Landed += OnHarpoonLanded;
        }

        public override void OnStopNetwork()
        {
            if (_harpoon != null) _harpoon.Landed -= OnHarpoonLanded;
            CorpsesLoaded.OnChange -= OnCorpsesChanged;
            base.OnStopNetwork();
        }

        private void OnCorpsesChanged(int prev, int next, bool asServer)
            => _signalBus?.Fire(new CannonChargeChangedSignal(next, RequiredCorpses));

        public override void OnStartServer()
        {
            base.OnStartServer();
            _targeting?.RegisterCannon(transform);
        }

        public override void OnStopServer()
        {
            _targeting?.UnregisterCannon(transform);
            base.OnStopServer();
        }

        private void Update()
        {
            if (IsServerInitialized && _pendingActive && Time.time >= _pendingApplyAt) ApplyPendingDamage();
            if (_turretPivot == null) return;

            if (IsLocalOperator)
            {
                var cfg = _configs?.Weapon;
                float resp = cfg != null ? Mathf.Max(0.1f, cfg.AimResponsiveness) : 6f;
                var target = Quaternion.Euler(_desiredPitch, _desiredYaw, 0f);
                _localAim = Quaternion.Slerp(_localAim, target, 1f - Mathf.Exp(-resp * Time.deltaTime));
                _turretPivot.localRotation = _localAim;
            }
            else
            {
                _turretPivot.localRotation = Quaternion.Slerp(
                    _turretPivot.localRotation, _aimRot.Value, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
        }

        public void BeginLocalAim()
        {
            _localAim = _turretPivot != null ? _turretPivot.localRotation : Quaternion.identity;
            var e = _localAim.eulerAngles;
            _desiredYaw = Mathf.DeltaAngle(0f, e.y);
            _desiredPitch = Mathf.DeltaAngle(0f, e.x);
        }

        public void DriveAim(Vector2 lookDelta)
        {
            var cfg = _configs?.Weapon;
            float yawSens = cfg != null ? cfg.AimYawSensitivity : 0.15f;
            float pitchSens = cfg != null ? cfg.AimPitchSensitivity : 0.12f;
            float yawLimit = cfg != null ? cfg.YawLimit : 70f;
            float pitchMin = cfg != null ? cfg.PitchMin : -45f;
            float pitchMax = cfg != null ? cfg.PitchMax : 20f;
            _desiredYaw = Mathf.Clamp(_desiredYaw + lookDelta.x * yawSens, -yawLimit, yawLimit);
            _desiredPitch = Mathf.Clamp(_desiredPitch - lookDelta.y * pitchSens, pitchMin, pitchMax);
        }

        public void ClientRequestMount() => ServerRequestMount();
        public void ClientRequestUnmount() => ServerRequestUnmount();

        public bool IsHarpoonReady => _harpoon == null || _harpoon.IsDocked;

        public void ClientFire()
        {
            if (_harpoon != null && !_harpoon.IsDocked) return;
            Vector3 origin = _muzzle != null ? _muzzle.position : transform.position;
            ServerFire(origin, ComputeAimPoint(origin));
        }

        private Vector3 ComputeAimPoint(Vector3 origin)
        {
            var cfg = _configs?.Weapon;
            float range = cfg != null ? cfg.MuzzleRange : 200f;
            Transform pivot = _cameraPivot != null ? _cameraPivot : CameraAnchor;
            Vector3 from = pivot != null ? pivot.position : origin;
            Vector3 dir = (_crosshairPoint != null && pivot != null)
                ? (_crosshairPoint.position - from)
                : (pivot != null ? pivot.forward : (_muzzle != null ? _muzzle.forward : transform.forward));
            if (dir.sqrMagnitude < 1e-6f) dir = transform.forward;
            dir.Normalize();
            return Physics.Raycast(from, dir, out var hit, range, _aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : from + dir * range;
        }

        public void ClientSubmitAim()
        {
            if (Quaternion.Angle(_localAim, _lastSentAim) < 1f) return;
            _lastSentAim = _localAim;
            SubmitAim(_localAim);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRequestMount(NetworkConnection conn = null)
        {
            if (conn == null || OperatorClientId.Value != -1 || !IsAssembled) return;
            OperatorClientId.Value = conn.ClientId;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerRequestUnmount(NetworkConnection conn = null)
        {
            if (conn == null || OperatorClientId.Value != conn.ClientId) return;
            OperatorClientId.Value = -1;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitAim(Quaternion rot, NetworkConnection conn = null)
        {
            if (conn == null || OperatorClientId.Value != conn.ClientId) return;
            _aimRot.Value = rot;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerFire(Vector3 origin, Vector3 target, NetworkConnection conn = null)
        {
            if (conn == null || OperatorClientId.Value != conn.ClientId) return;
            if (Time.time < _harpoonBusyUntil) return;
            var cfg = _configs?.Weapon;
            if (cfg == null) return;

            Vector3 toTarget = target - origin;
            float dist = toTarget.magnitude;
            if (dist < 0.05f) return;
            Vector3 dir = toTarget / dist;

            Vector3 landing = target;
            Source hitSource = null;
            Enemy hitEnemy = null;
            if (Physics.Raycast(origin, dir, out var rh, dist + 0.5f, _aimMask, QueryTriggerInteraction.Ignore))
            {
                landing = rh.point;
                hitSource = rh.collider.GetComponentInParent<Source>();
                hitEnemy = rh.collider.GetComponentInParent<Enemy>();
            }

            float flight = _harpoon != null ? _harpoon.EstimateFlightSeconds(origin, landing) : 0.4f;
            float cycle = _harpoon != null ? _harpoon.EstimateCycleSeconds(origin, landing) : Mathf.Max(0.5f, cfg.FireCooldownSec);
            _harpoonBusyUntil = Time.time + cycle;

            _pendingSource = hitSource;
            _pendingEnemy = hitEnemy;
            _pendingDamage = cfg.ShotDamage;
            _pendingApplyAt = Time.time + flight;
            _pendingActive = true;

            RpcLaunchHarpoon(origin, landing);
        }

        private void ApplyPendingDamage()
        {
            _pendingActive = false;
            if (_pendingSource != null && _pendingSource.IsVulnerable.Value && !_pendingSource.Destroyed.Value && IsCharged)
                _pendingSource.ServerApplyDamage(_pendingDamage);
            if (_pendingEnemy != null)
                _pendingEnemy.ServerApplyDamage(_pendingDamage);
            _pendingSource = null;
            _pendingEnemy = null;
        }

        [ObserversRpc]
        private void RpcLaunchHarpoon(Vector3 origin, Vector3 landing)
        {
            _signalBus?.Fire(new WeaponFiredSignal(origin, landing, false));
            _harpoon?.Launch(origin, landing);
        }

        private void OnHarpoonLanded(Vector3 point) => _signalBus?.Fire(new HarpoonImpactSignal(point));
    }
}
