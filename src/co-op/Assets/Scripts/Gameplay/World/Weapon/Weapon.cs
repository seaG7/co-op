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
        [SerializeField, HideInInspector] private List<WeaponSnapPoint> _snapPoints = new();

        [Header("Turret")]
        [SerializeField] private Transform _turretPivot;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _cameraAnchor;

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
        private float _cooldownLeft;

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
            for (int i = 0; i < _snapPoints.Count; i++)
                if (_snapPoints[i] != null) _snapPoints[i].IsOccupied.Value = true;
        }

        public bool IsAssembled
        {
            get
            {
                if (_snapPoints.Count == 0) return false;
                for (int i = 0; i < _snapPoints.Count; i++)
                    if (_snapPoints[i] == null || _snapPoints[i].IsFree) return false;
                return true;
            }
        }

        private void Awake()
        {
            _snapPoints.Clear();
            _snapPoints.AddRange(GetComponentsInChildren<WeaponSnapPoint>(includeInactive: true));
            if (_turretPivot == null) _turretPivot = transform.Find("Turret");
            var pivot = _turretPivot != null ? _turretPivot : transform;
            if (_muzzle == null) _muzzle = pivot.Find("Muzzle");
            if (_cameraAnchor == null) _cameraAnchor = pivot.Find("CameraAnchor");
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            CorpsesLoaded.OnChange += OnCorpsesChanged;
        }

        public override void OnStopNetwork()
        {
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
            if (IsServerInitialized && _cooldownLeft > 0f) _cooldownLeft -= Time.deltaTime;
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

        public void ClientFire()
        {
            if (_muzzle == null) return;
            ServerFire(_muzzle.position, _muzzle.forward);
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
        private void ServerFire(Vector3 origin, Vector3 dir, NetworkConnection conn = null)
        {
            if (conn == null || OperatorClientId.Value != conn.ClientId) return;
            var cfg = _configs?.Weapon;
            if (cfg == null || _cooldownLeft > 0f) return;
            _cooldownLeft = Mathf.Max(0.05f, cfg.FireCooldownSec);

            bool hit = false;
            Vector3 hitPoint = origin + dir.normalized * cfg.MuzzleRange;
            if (Physics.Raycast(origin, dir, out var rh, cfg.MuzzleRange, ~0, QueryTriggerInteraction.Ignore))
            {
                hitPoint = rh.point;
                var src = rh.collider.GetComponentInParent<Source>();
                if (src != null && src.IsVulnerable.Value && !src.Destroyed.Value && IsCharged)
                {
                    src.ServerApplyDamage(cfg.ShotDamage);
                    hit = true;
                }

                var enemy = rh.collider.GetComponentInParent<Enemy>();
                if (enemy != null)
                {
                    enemy.ServerApplyDamage(cfg.ShotDamage);
                    hit = true;
                }
            }
            RpcFired(origin, hitPoint, hit);
        }

        [ObserversRpc]
        private void RpcFired(Vector3 origin, Vector3 hitPoint, bool hit)
            => _signalBus?.Fire(new WeaponFiredSignal(origin, hitPoint, hit));
    }
}
