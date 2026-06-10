using FishNet.Object;
using Gameplay.Player.Look;
using Infrastructure.Services.Camera;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Camera
{
    public class PlayerCameraRig : NetworkBehaviour
    {
        [Header("Refs")]
        [SerializeField] private UnityEngine.Camera _camera;

        [Tooltip("Local position of the camera relative to the player root (head-height for first-person). " +
                 "For a height-2 capsule with pivot at its base, 0.85 m puts the eye just below the top.")]
        [SerializeField] private Vector3 _localCameraOffset = new Vector3(0f, 0.85f, 0f);

        [Header("Downed (knocked-down) external view")]
        [Tooltip("Camera position relative to the player while downed — pulled out to the side/behind so you see your own body and feel you've lost control.")]
        [SerializeField] private Vector3 _downedCameraOffset = new Vector3(3f, 1.8f, -1f);

        [Tooltip("Local point the downed camera frames (roughly the player's chest).")]
        [SerializeField] private Vector3 _downedLookTarget = new Vector3(0f, 1f, 0f);

        [Tooltip("Seconds to ease between first-person and the downed view (both directions).")]
        [SerializeField] private float _blendDuration = 0.4f;

        [Header("Camera shake")]
        [SerializeField] private float _shakeMaxOffset = 0.25f;
        [SerializeField] private float _shakeDecay = 1.8f;
        [SerializeField] private float _shakeFrequency = 22f;

        [Inject] private ICameraService _cameraService;

        private bool _active;
        private bool _downed;
        private PlayerLookController _look;
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
            _camera.transform.SetParent(anchor, worldPositionStays: false);
            _camera.transform.localPosition = Vector3.zero;
            _camera.transform.localRotation = Quaternion.identity;
        }

        public void Restore()
        {
            if (!base.IsOwner || _camera == null) return;
            _downed = false;
            _blending = false;
            _spectating = false;
            _camera.transform.SetParent(transform, worldPositionStays: false);
            _camera.transform.localPosition = _localCameraOffset;
            _camera.transform.localRotation = Quaternion.identity;
        }

        public void SetDownedView(bool on)
        {
            if (!base.IsOwner || _camera == null) return;
            _spectating = false;
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

            Transform t = _camera.transform;
            t.localPosition -= _shakeOffset;
            if (_trauma > 0f)
            {
                _trauma = Mathf.Max(0f, _trauma - _shakeDecay * Time.deltaTime);
                float s = _trauma * _trauma;
                float tt = Time.time * _shakeFrequency;
                _shakeOffset = new Vector3(
                    Mathf.PerlinNoise(_shakeSeed, tt) - 0.5f,
                    Mathf.PerlinNoise(_shakeSeed + 11f, tt) - 0.5f,
                    Mathf.PerlinNoise(_shakeSeed + 23f, tt) - 0.5f) * (2f * s * _shakeMaxOffset);
            }
            else
            {
                _shakeOffset = Vector3.zero;
            }
            t.localPosition += _shakeOffset;
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
