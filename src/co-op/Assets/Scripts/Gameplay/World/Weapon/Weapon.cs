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

        [Header("Turret (2-axis)")]
        [Tooltip("Rotates horizontally (yaw). Parent of the pitch pivot; holds the parts that swing left/right.")]
        [SerializeField] private Transform _yawPivot;
        [Tooltip("Local axis the yaw pivot spins around. Default Y (up).")]
        [SerializeField] private Vector3 _yawAxis = Vector3.up;
        [Tooltip("Rotates vertically (pitch / elevation). Child of the yaw pivot; holds the barrel/harpoon.")]
        [SerializeField] private Transform _pitchPivot;
        [Tooltip("Local axis the pitch pivot spins around for up/down. Default Z (forward) — set X (right) if your barrel elevates around X.")]
        [SerializeField] private Vector3 _pitchAxis = Vector3.forward;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _cameraAnchor;
        [Tooltip("Handles the operator's hands IK to while mounted (children of the pitch pivot; named GripL/GripR, else auto-created at a default — move them onto the real handles).")]
        [SerializeField] private Transform _leftGrip;
        [SerializeField] private Transform _rightGrip;

        [Header("Harpoon")]
        [SerializeField] private Harpoon _harpoon;
        [Tooltip("3D crosshair/reticle point on the turret. The camera centers on it; the aim ray goes from the camera anchor through it.")]
        [SerializeField] private Transform _crosshairPoint;
        [Tooltip("Layers the aim/fire ray may hit. The cannon's own colliders are skipped automatically.")]
        [SerializeField] private LayerMask _aimMask = ~0;

        [Header("Operator")]
        [Tooltip("Spot the player must stand in to mount (e.g. an empty behind the cannon). Null = mount from anywhere within look-range.")]
        [SerializeField] private Transform _operatorStand;
        [Tooltip("How close (m) to the operator stand the player must be to mount.")]
        [SerializeField] private float _operatorRadius = 1.5f;

        [Inject] private IConfigDataProvider _configs;
        [Inject] private SignalBus _signalBus;
        [Inject] private IEnemyTargetingService _targeting;

        public readonly SyncVar<int> OperatorClientId = new(-1);
        public readonly SyncVar<int> CorpsesLoaded = new(0);
        private readonly SyncVar<float> _aimYaw = new(0f);
        private readonly SyncVar<float> _aimPitch = new(0f);

        private float _desiredYaw;
        private float _desiredPitch;
        private float _localYaw;
        private float _localPitch;
        private float _lastSentYaw;
        private float _lastSentPitch;

        private float _harpoonBusyUntil;
        private Source _pendingSource;
        private Enemy _pendingEnemy;
        private float _pendingDamage;
        private float _pendingApplyAt;
        private bool _pendingActive;
        private bool _wasAssembled;

        public Transform CameraAnchor =>
            _cameraAnchor != null ? _cameraAnchor : (_pitchPivot != null ? _pitchPivot : (_yawPivot != null ? _yawPivot : transform));

        public Transform CrosshairPoint => _crosshairPoint;
        public Transform GripLeft => _leftGrip;
        public Transform GripRight => _rightGrip;

        public bool CanMountFrom(Vector3 playerPos)
            => _operatorStand == null || (playerPos - _operatorStand.position).sqrMagnitude <= _operatorRadius * _operatorRadius;

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
            if (_yawPivot == null) _yawPivot = transform.Find("YawPivot");
            if (_pitchPivot == null) _pitchPivot = _yawPivot != null ? _yawPivot.Find("PitchPivot") : transform.Find("PitchPivot");
            var aimRoot = _pitchPivot != null ? _pitchPivot : (_yawPivot != null ? _yawPivot : transform);
            if (_muzzle == null) _muzzle = aimRoot.Find("Muzzle");
            if (_cameraAnchor == null) _cameraAnchor = aimRoot.Find("CameraAnchor");
            if (_harpoon == null) _harpoon = GetComponentInChildren<Harpoon>(includeInactive: true);
            if (_leftGrip == null) _leftGrip = aimRoot.Find("GripL");
            if (_rightGrip == null) _rightGrip = aimRoot.Find("GripR");
            if (_leftGrip == null) _leftGrip = MakeGrip(aimRoot, "GripL", -0.22f);
            if (_rightGrip == null) _rightGrip = MakeGrip(aimRoot, "GripR", 0.22f);
        }

        private Transform MakeGrip(Transform parent, string name, float x)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Vector3 baseLocal = _cameraAnchor != null && _cameraAnchor.parent == parent ? _cameraAnchor.localPosition : Vector3.zero;
            go.transform.localPosition = baseLocal + new Vector3(x, -0.2f, 0.12f);
            return go.transform;
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
            _wasAssembled = IsAssembled;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null) _slots[i].IsOccupied.OnChange += OnSlotOccupancyChanged;
        }

        public override void OnStopServer()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i] != null) _slots[i].IsOccupied.OnChange -= OnSlotOccupancyChanged;
            _targeting?.UnregisterCannon(transform);
            base.OnStopServer();
        }

        private void Update()
        {
            if (IsServerInitialized && _pendingActive && Time.time >= _pendingApplyAt) ApplyPendingDamage();

            if (IsLocalOperator)
            {
                var cfg = _configs?.Weapon;
                float resp = cfg != null ? Mathf.Max(0.1f, cfg.AimResponsiveness) : 6f;
                float k = 1f - Mathf.Exp(-resp * Time.deltaTime);
                _localYaw = Mathf.Lerp(_localYaw, _desiredYaw, k);
                _localPitch = Mathf.Lerp(_localPitch, _desiredPitch, k);
            }
            else
            {
                float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
                _localYaw = Mathf.LerpAngle(_localYaw, _aimYaw.Value, k);
                _localPitch = Mathf.LerpAngle(_localPitch, _aimPitch.Value, k);
            }

            if (_yawPivot != null) _yawPivot.localRotation = Quaternion.AngleAxis(_localYaw, _yawAxis.sqrMagnitude > 1e-6f ? _yawAxis : Vector3.up);
            if (_pitchPivot != null) _pitchPivot.localRotation = Quaternion.AngleAxis(_localPitch, _pitchAxis.sqrMagnitude > 1e-6f ? _pitchAxis : Vector3.forward);
        }

        public void BeginLocalAim()
        {
            _desiredYaw = _localYaw;
            _desiredPitch = _localPitch;
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
            _desiredPitch = Mathf.Clamp(_desiredPitch + lookDelta.y * pitchSens, pitchMin, pitchMax);
        }

        public void ClientRequestMount() => ServerRequestMount();
        public void ClientRequestUnmount() => ServerRequestUnmount();

        public bool IsHarpoonReady => _harpoon == null || _harpoon.IsDocked;

        public void ClientFire()
        {
            if (_harpoon != null && !_harpoon.IsDocked) return;
            Vector3 origin = _harpoon != null ? _harpoon.NoseWorldPosition : (_muzzle != null ? _muzzle.position : transform.position);
            ServerFire(origin, ComputeAimPoint(origin));
        }

        private Vector3 ComputeAimPoint(Vector3 origin)
        {
            var cfg = _configs?.Weapon;
            float range = cfg != null ? cfg.MuzzleRange : 200f;

            // Aim STRICTLY along the cannon barrel through the 3D crosshair marker — NOT the camera /
            // HUD screen centre. The ray starts at the harpoon nose (origin), so the shot always
            // travels forward down the barrel.
            Vector3 barrelFwd = _muzzle != null ? _muzzle.forward
                : (_pitchPivot != null ? _pitchPivot.forward : transform.forward);

            // Use the crosshair OBJECT's forward (its rotation is the authored aim axis) — NOT
            // (crosshair.position - nose), which drifts sideways when the marker is offset laterally.
            Vector3 dir = _crosshairPoint != null ? _crosshairPoint.forward : barrelFwd;

            // Guard against a mis-oriented crosshair (facing backwards) that would fire backwards.
            if (dir.sqrMagnitude < 1e-6f || Vector3.Dot(dir, barrelFwd) <= 0f)
                dir = barrelFwd;
            dir.Normalize();

            return RaycastIgnoringSelf(origin, dir, range, out var hit) ? hit.point : origin + dir * range;
        }

        private bool RaycastIgnoringSelf(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit best)
        {
            best = default;
            var hits = Physics.RaycastAll(origin, dir, maxDist, _aimMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.GetComponentInParent<Weapon>() == this) continue;
                if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = hits[i]; found = true; }
            }
            return found;
        }

        public void ClientSubmitAim()
        {
            if (Mathf.Abs(_localYaw - _lastSentYaw) < 0.5f && Mathf.Abs(_localPitch - _lastSentPitch) < 0.5f) return;
            _lastSentYaw = _localYaw;
            _lastSentPitch = _localPitch;
            SubmitAim(_localYaw, _localPitch);
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
        private void SubmitAim(float yaw, float pitch, NetworkConnection conn = null)
        {
            if (conn == null || OperatorClientId.Value != conn.ClientId) return;
            _aimYaw.Value = yaw;
            _aimPitch.Value = pitch;
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
            if (RaycastIgnoringSelf(origin, dir, dist + 0.5f, out var rh))
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

        private void OnSlotOccupancyChanged(bool prev, bool next, bool asServer)
        {
            bool now = IsAssembled;
            if (now && !_wasAssembled) { _wasAssembled = true; RpcAssembled(transform.position); }
            else if (!now) _wasAssembled = false;
        }

        [ObserversRpc]
        private void RpcAssembled(Vector3 pos) => _signalBus?.Fire(new WeaponAssembledSignal(pos));
    }
}
