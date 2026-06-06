using FishNet.Object;
using FishNet.Object.Synchronizing;
using Gameplay.Player.Camera;
using Infrastructure.Services.Input;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Look
{
    public class PlayerLookController : NetworkBehaviour
    {
        [Header("Sensitivity")]
        [SerializeField] private float _yawSensitivity = 0.15f;
        [SerializeField] private float _pitchSensitivity = 0.12f;

        [Header("Pitch clamp (degrees, symmetric)")]
        [SerializeField] private float _pitchClamp = 85f;

        [Header("Refs")]
        [SerializeField] private PlayerCameraRig _cameraRig;
        [Tooltip("Eye/aim origin in player-local space (≈ camera height). Used for the replicated aim ray that the carry system reads on the server.")]
        [SerializeField] private Vector3 _eyeLocalOffset = new Vector3(0f, 0.85f, 0f);

        [Inject] private IInputService _input;

        private readonly SyncVar<float> _netPitch = new(0f);

        private float _pitch;
        private float _lastSentPitch;

        public Vector3 EyePosition => transform.TransformPoint(_eyeLocalOffset);

        public Vector3 AimDirection
        {
            get
            {
                float pitch = base.IsOwner ? _pitch : _netPitch.Value;
                return transform.rotation * Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;
            }
        }

        private void Update()
        {
            if (!base.IsOwner || _input == null) return;
            var look = _input.LookAxis;
            if (look.sqrMagnitude < 0.0001f) return;

            transform.Rotate(0f, look.x * _yawSensitivity, 0f, Space.Self);

            _pitch = Mathf.Clamp(_pitch - look.y * _pitchSensitivity, -_pitchClamp, _pitchClamp);

            if (Mathf.Abs(_pitch - _lastSentPitch) > 0.5f)
            {
                _lastSentPitch = _pitch;
                SubmitPitch(_pitch);
            }

            if (_cameraRig != null && _cameraRig.Camera != null)
                _cameraRig.Camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        [ServerRpc]
        private void SubmitPitch(float pitch) => _netPitch.Value = Mathf.Clamp(pitch, -_pitchClamp, _pitchClamp);
    }
}
