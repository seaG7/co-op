using FishNet.Object;
using Gameplay.Player.Look;
using Gameplay.Player.Movement;
using Gameplay.Player.Vitals;
using Infrastructure.Services.Camera;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Camera
{
    public class PlayerCameraRig : NetworkBehaviour
    {
        [Header("Refs")]
        [SerializeField] private UnityEngine.Camera _camera;

        [SerializeField] private Vector3 _localCameraOffset = new Vector3(0f, 0.85f, 0f);

        [Header("Downed (knocked-down) external view")]
        [SerializeField] private Vector3 _downedCameraOffset = new Vector3(3f, 1.8f, -1f);
        [SerializeField] private Vector3 _downedLookTarget = new Vector3(0f, 1f, 0f);
        [SerializeField] private float _blendDuration = 0.4f;

        [Header("Camera shake")]
        [SerializeField] private float _shakeMaxOffset = 0.25f;
        [SerializeField] private float _shakeDecay = 1.8f;
        [SerializeField] private float _shakeFrequency = 22f;

        [Header("Step bob")]
        [SerializeField] private float _bobAmplitudeY = 0.03f;
        [SerializeField] private float _bobAmplitudeX = 0.018f;
        [SerializeField] private float _bobSpeedReference = 5f;
        [SerializeField] private float _bobSmoothing = 12f;

        [Header("Idle sway")]
        [SerializeField] private float _idleSwayAmount = 0.015f;
        [SerializeField] private float _idleSwaySpeed = 1.2f;

        [Header("Strafe roll")]
        [SerializeField] private float _strafeRollPerSpeed = 0.6f;
        [SerializeField] private float _strafeRollMax = 2.5f;
        [SerializeField] private float _strafeRollSmoothing = 8f;

        [Header("Landing / FOV")]
        [SerializeField] private float _landDipAmount = 0.12f;
        [SerializeField] private float _landDipDuration = 0.25f;
        [SerializeField] private float _landFovPunch = 6f;
        [SerializeField] private float _fovPunchDecay = 18f;
        [SerializeField] private float _moveFovAdd = 2.5f;
        [SerializeField] private float _fovSmoothing = 8f;

        [Header("Drunk (scaled by PlayerDrunk.Intensity — tune/extend freely)")]
        [SerializeField] private float _drunkSway = 0.06f;
        [SerializeField] private float _drunkRoll = 5f;
        [SerializeField] private float _drunkFov = 5f;
        [SerializeField] private float _drunkSpeed = 1.4f;

        [Inject] private ICameraService _cameraService;

        private bool _active;
        private bool _downed;
        private PlayerLookController _look;
        private PlayerMovement _movement;
        private PlayerDrunk _drunk;
        private bool _blending;
        private bool _toDowned;
        private float _blendElapsed;
        private Vector3 _fromPos;
        private Quaternion _fromRot;
        private float _trauma;
        private float _shakeSeed;
        private Vector3 _shakeOffset;
        private Transform _spectateTarget;
        private Vector3 _spectateOffset;
        private bool _spectating;
        private bool _mountAiming;
        private Transform _mountLookTarget;

        private Vector3 _bobOffset;
        private Vector3 _swayOffset;
        private float _rollAngle;
        private float _dipTime;
        private float _fovPunch;
        private float _baseFov = 60f;
        private float _currentFov = 60f;

        public static PlayerCameraRig Local { get; private set; }

        public UnityEngine.Camera Camera => _camera;

        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        public void SpectateFollow(Transform target, Vector3 followOffset)
        {
            if (!base.IsOwner || _camera == null) return;
            _downed = false;
            _blending = false;
            _spectateTarget = target;
            _spectateOffset = followOffset;
            _spectating = target != null;
            if (!_spectating)
            {
                _camera.transform.SetParent(transform, worldPositionStays: false);
                _camera.transform.localPosition = _localCameraOffset;
                _camera.transform.localRotation = Quaternion.identity;
                return;
            }
            _camera.transform.SetParent(null, worldPositionStays: true);
        }

        public void MountTo(Transform anchor)
        {
            if (!base.IsOwner || _camera == null || anchor == null) return;
            _downed = false;
            _blending = false;
            _spectating = false;
            _mountAiming = false;
            _mountLookTarget = null;
            _camera.transform.SetParent(anchor, worldPositionStays: false);
            _camera.transform.localPosition = Vector3.zero;
            _camera.transform.localRotation = Quaternion.identity;
        }

        public void MountLookAt(Transform cameraPose, Transform lookTarget)
        {
            if (!base.IsOwner || _camera == null || cameraPose == null) return;
            _downed = false;
            _blending = false;
            _spectating = false;
            _mountAiming = true;
            _mountLookTarget = lookTarget;
            _camera.transform.SetParent(cameraPose, worldPositionStays: false);
            _camera.transform.localPosition = Vector3.zero;
            _camera.transform.localRotation = Quaternion.identity;
        }

        public void Restore()
        {
            if (!base.IsOwner || _camera == null) return;
            _downed = false;
            _blending = false;
            _spectating = false;
            _mountAiming = false;
            _mountLookTarget = null;
            _camera.transform.SetParent(transform, worldPositionStays: false);
            _camera.transform.localPosition = _localCameraOffset;
            _camera.transform.localRotation = Quaternion.identity;
        }

        public void SetDownedView(bool on)
        {
            if (!base.IsOwner || _camera == null) return;
            _spectating = false;
            _mountAiming = false;
            _mountLookTarget = null;
            _camera.transform.SetParent(transform, worldPositionStays: false);

            if (on)
            {
                _downed = true;
                if (_look != null) _look.enabled = false;
                BeginBlend(toDowned: true);
            }
            else if (_downed)
            {
                _downed = false;
                if (_look != null) _look.enabled = false;
                BeginBlend(toDowned: false);
            }
            else
            {
                _blending = false;
                _camera.transform.localPosition = _localCameraOffset;
                _camera.transform.localRotation = Quaternion.identity;
                if (_look != null) _look.enabled = true;
            }
        }

        private void BeginBlend(bool toDowned)
        {
            _blending = true;
            _toDowned = toDowned;
            _blendElapsed = 0f;
            _fromPos = _camera.transform.localPosition;
            _fromRot = _camera.transform.localRotation;
        }

        private void Update()
        {
            if (!_blending || _camera == null || !base.IsOwner) return;

            _blendElapsed += Time.deltaTime;
            float t = _blendDuration > 0f ? Mathf.Clamp01(_blendElapsed / _blendDuration) : 1f;
            float s = t * t * (3f - 2f * t);

            Vector3 targetPos;
            Quaternion targetRot;
            if (_toDowned)
            {
                targetPos = _downedCameraOffset;
                Vector3 dir = _downedLookTarget - targetPos;
                targetRot = dir.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(dir, Vector3.up)
                    : Quaternion.identity;
            }
            else
            {
                targetPos = _localCameraOffset;
                targetRot = Quaternion.Euler(_look != null ? _look.Pitch : 0f, 0f, 0f);
            }

            Transform tr = _camera.transform;
            tr.localPosition = Vector3.Lerp(_fromPos, targetPos, s);
            tr.localRotation = Quaternion.Slerp(_fromRot, targetRot, s);

            if (t >= 1f)
            {
                _blending = false;
                tr.localPosition = targetPos;
                tr.localRotation = targetRot;
                if (!_toDowned && _look != null) _look.enabled = true;
            }
        }

        private void LateUpdate()
        {
            if (!base.IsOwner || _camera == null) return;

            if (_spectating && _spectateTarget != null)
            {
                FollowSpectate();
                return;
            }

            if (_mountAiming && _mountLookTarget != null)
            {
                _shakeOffset = ComputeShake();
                _camera.transform.localPosition = _shakeOffset;
                Vector3 lookDir = _mountLookTarget.position - _camera.transform.position;
                if (lookDir.sqrMagnitude > 1e-5f)
                    _camera.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
                return;
            }

            Transform t = _camera.transform;
            if (t.parent == transform && !_blending && !_downed)
            {
                ApplyFeel(t);
                return;
            }

            t.localPosition -= _shakeOffset;
            _shakeOffset = ComputeShake();
            t.localPosition += _shakeOffset;
        }

        private Vector3 ComputeShake()
        {
            if (_trauma <= 0f) return Vector3.zero;
            _trauma = Mathf.Max(0f, _trauma - _shakeDecay * Time.deltaTime);
            float s = _trauma * _trauma;
            float tt = Time.time * _shakeFrequency;
            return new Vector3(
                Mathf.PerlinNoise(_shakeSeed, tt) - 0.5f,
                Mathf.PerlinNoise(_shakeSeed + 11f, tt) - 0.5f,
                Mathf.PerlinNoise(_shakeSeed + 23f, tt) - 0.5f) * (2f * s * _shakeMaxOffset);
        }

        private void ApplyFeel(Transform t)
        {
            float dt = Time.deltaTime;
            _shakeOffset = ComputeShake();

            float speed = 0f, lateral = 0f, phase = 0f;
            bool grounded = true;
            if (_movement != null)
            {
                var snap = _movement.Snapshot;
                speed = snap.HorizontalSpeed;
                lateral = snap.LocalVelocity.x;
                grounded = snap.IsGrounded;
                phase = _movement.StepPhase;
                if (snap.WasJustGrounded) { _dipTime = _landDipDuration; _fovPunch = _landFovPunch; }
            }

            float moveAmt = Mathf.Clamp01(speed / Mathf.Max(0.01f, _bobSpeedReference));

            Vector3 bob = Vector3.zero;
            if (grounded && moveAmt > 0.01f)
            {
                bob.y = Mathf.Sin(phase * Mathf.PI * 4f) * _bobAmplitudeY * moveAmt;
                bob.x = Mathf.Sin(phase * Mathf.PI * 2f) * _bobAmplitudeX * moveAmt;
            }
            _bobOffset = Vector3.Lerp(_bobOffset, bob, 1f - Mathf.Exp(-_bobSmoothing * dt));

            Vector3 sway = Vector3.zero;
            if (moveAmt <= 0.05f)
            {
                float ts = Time.time;
                sway.x = Mathf.Sin(ts * _idleSwaySpeed) * _idleSwayAmount;
                sway.y = Mathf.Sin(ts * _idleSwaySpeed * 1.7f) * _idleSwayAmount * 0.6f;
            }
            _swayOffset = Vector3.Lerp(_swayOffset, sway, 1f - Mathf.Exp(-3f * dt));

            Vector3 dip = Vector3.zero;
            if (_dipTime > 0f)
            {
                _dipTime -= dt;
                float k = Mathf.Clamp01(_dipTime / Mathf.Max(0.01f, _landDipDuration));
                dip.y = -_landDipAmount * k;
            }

            float targetRoll = Mathf.Clamp(-lateral * _strafeRollPerSpeed, -_strafeRollMax, _strafeRollMax);
            _rollAngle = Mathf.Lerp(_rollAngle, targetRoll, 1f - Mathf.Exp(-_strafeRollSmoothing * dt));

            if (_fovPunch > 0f) _fovPunch = Mathf.Max(0f, _fovPunch - _fovPunchDecay * dt);
            float targetFov = _baseFov + _fovPunch + moveAmt * _moveFovAdd;
            _currentFov = Mathf.Lerp(_currentFov, targetFov, 1f - Mathf.Exp(-_fovSmoothing * dt));

            float drunk = _drunk != null ? _drunk.Intensity : 0f;
            if (drunk > 0.01f)
            {
                float ts = Time.time;
                _bobOffset += new Vector3(
                    Mathf.Sin(ts * _drunkSpeed) * _drunkSway * drunk,
                    Mathf.Sin(ts * _drunkSpeed * 1.3f + 1.1f) * _drunkSway * 0.6f * drunk,
                    0f);
                _rollAngle += Mathf.Sin(ts * _drunkSpeed * 0.7f) * _drunkRoll * drunk;
                _currentFov += Mathf.Sin(ts * _drunkSpeed * 0.5f) * _drunkFov * drunk;
            }

            t.localPosition = _localCameraOffset + _bobOffset + _swayOffset + dip + _shakeOffset;
            float pitch = _look != null ? _look.Pitch : 0f;
            t.localRotation = Quaternion.Euler(pitch, 0f, _rollAngle);
            _camera.fieldOfView = _currentFov;
        }

        private void FollowSpectate()
        {
            Transform cam = _camera.transform;
            Vector3 desired = _spectateTarget.position + _spectateTarget.TransformDirection(_spectateOffset);
            Vector3 look = _spectateTarget.position + Vector3.up * 1.2f;
            cam.position = Vector3.Lerp(cam.position, desired, 1f - Mathf.Exp(-6f * Time.deltaTime));
            Vector3 dir = look - cam.position;
            if (dir.sqrMagnitude > 1e-4f)
                cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!base.IsOwner) return;

            _camera = _cameraService?.ResolveCamera();
            if (_camera == null)
            {
                Debug.LogError($"[{nameof(PlayerCameraRig)}] No camera available from ICameraService. " +
                               "Assign GameSceneInstaller._gameCamera or tag a camera 'MainCamera'.", this);
                return;
            }

            _camera.transform.SetParent(transform, worldPositionStays: false);
            _camera.transform.localPosition = _localCameraOffset;
            _camera.transform.localRotation = Quaternion.identity;

            _look = GetComponent<PlayerLookController>();
            _movement = GetComponent<PlayerMovement>();
            _drunk = GetComponent<PlayerDrunk>();
            _baseFov = _camera.fieldOfView;
            _currentFov = _baseFov;
            _shakeSeed = Random.value * 100f;
            Local = this;
            _active = true;
        }

        public override void OnStopClient()
        {
            if (base.IsOwner)
            {
                _active = false;
                if (Local == this) Local = null;
                if (_camera != null)
                    _camera.transform.SetParent(null, worldPositionStays: true);
            }
            base.OnStopClient();
        }
    }
}
