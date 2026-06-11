using FishNet.Object;
using Gameplay.Net;
using Gameplay.Player.Camera;
using Gameplay.Player.Carry;
using Gameplay.Player.Look;
using Gameplay.Player.Movement;
using Gameplay.Player.Vitals;
using Gameplay.Player.View;
using Gameplay.World.Weapon;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Input;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Weapons
{
    public sealed class PlayerWeaponControl : NetworkBehaviour, IRuntimeInjectionListener
    {
        [SerializeField] private LayerMask _weaponMask = ~0;

        private PlayerCameraRig _cameraRig;

        [Inject] private IInputService _input;
        [Inject] private IConfigDataProvider _configs;
        [Inject] private SignalBus _signalBus;

        private PlayerLookController _look;
        private PlayerMovement _movement;
        private PlayerCarry _carry;
        private PlayerVitals _vitals;
        private PlayerModelVisibility _modelVis;

        private Weapon _weapon;
        private bool _mounted;
        private bool _inputBound;

        public bool IsMounted => _mounted;
        public Transform MountGripLeft => _mounted && _weapon != null ? _weapon.GripLeft : null;
        public Transform MountGripRight => _mounted && _weapon != null ? _weapon.GripRight : null;

        private void Awake()
        {
            _look = GetComponent<PlayerLookController>();
            _movement = GetComponent<PlayerMovement>();
            _carry = GetComponent<PlayerCarry>();
            _vitals = GetComponent<PlayerVitals>();
            _cameraRig = GetComponent<PlayerCameraRig>();
            _modelVis = GetComponent<PlayerModelVisibility>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner) BindInput();
        }

        public void OnRuntimeInjected()
        {
            if (base.IsOwner) BindInput();
        }

        public override void OnStopClient()
        {
            if (base.IsOwner)
            {
                if (_mounted) ExitAimMode();
                UnbindInput();
            }
            base.OnStopClient();
        }

        private void OnDestroy() => UnbindInput();

        private void BindInput()
        {
            if (_inputBound || _input == null) return;
            _input.InteractStarted += OnInteract;
            _input.FireStarted += OnFire;
            _inputBound = true;
        }

        private void UnbindInput()
        {
            if (!_inputBound || _input == null) return;
            _input.InteractStarted -= OnInteract;
            _input.FireStarted -= OnFire;
            _inputBound = false;
        }

        private void OnInteract()
        {
            if (!base.IsOwner) return;
            if (_mounted) { _weapon?.ClientRequestUnmount(); return; }
            TryMount();
        }

        private void OnFire()
        {
            if (!base.IsOwner || !_mounted || _weapon == null) return;
            _weapon.ClientFire();
        }

        private void TryMount()
        {
            if (_vitals != null && !_vitals.IsAlive) return;
            if (_carry != null && _carry.IsHolding) return;
            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null) return;

            float range = _configs?.Weapon != null ? _configs.Weapon.MountRange : 3f;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit,
                    range + 2f, _weaponMask, QueryTriggerInteraction.Ignore))
                return;

            var weapon = hit.collider.GetComponentInParent<Weapon>();
            if (weapon == null || !weapon.IsFree || !weapon.IsAssembled) return;
            if (Vector3.Distance(transform.position, weapon.transform.position) > range + 2f) return;
            if (!weapon.CanMountFrom(transform.position)) return;

            _weapon = weapon;
            weapon.ClientRequestMount();
        }

        private void Update()
        {
            if (!base.IsOwner) return;

            bool shouldBeMounted = _weapon != null && _weapon.IsLocalOperator;
            if (shouldBeMounted && !_mounted) EnterAimMode();
            else if (!shouldBeMounted && _mounted) ExitAimMode();

            if (_mounted && _weapon != null)
            {
                _weapon.DriveAim(_input != null ? _input.LookAxis : Vector2.zero);
                _weapon.ClientSubmitAim();
            }
        }

        private void EnterAimMode()
        {
            _mounted = true;
            _weapon.BeginLocalAim();
            if (_look != null) _look.enabled = false;
            if (_movement != null) _movement.enabled = false;
            if (_carry != null) _carry.SetInteractionSuppressed(true);
            if (_modelVis != null) _modelVis.SetOwnerModelHidden(true);
            if (_cameraRig != null) _cameraRig.MountLookAt(_weapon.CameraAnchor, _weapon.CrosshairPoint);
            _signalBus?.Fire(new WeaponMountedSignal(true));
        }

        private void ExitAimMode()
        {
            _mounted = false;
            if (_cameraRig != null) _cameraRig.Restore();
            if (_look != null) _look.enabled = true;
            if (_movement != null) _movement.enabled = true;
            if (_carry != null) _carry.SetInteractionSuppressed(false);
            if (_modelVis != null) _modelVis.SetOwnerModelHidden(false);
            _weapon = null;
            _signalBus?.Fire(new WeaponMountedSignal(false));
        }
    }
}
