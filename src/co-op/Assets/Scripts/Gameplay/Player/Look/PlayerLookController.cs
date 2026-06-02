using FishNet.Object;
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

        [Inject] private IInputService _input;

        private float _pitch;

        private void Update()
        {
            if (!base.IsOwner || _input == null) return;
            var look = _input.LookAxis;
            if (look.sqrMagnitude < 0.0001f) return;

            transform.Rotate(0f, look.x * _yawSensitivity, 0f, Space.Self);

            _pitch = Mathf.Clamp(_pitch - look.y * _pitchSensitivity, -_pitchClamp, _pitchClamp);

            if (_cameraRig != null && _cameraRig.Camera != null)
                _cameraRig.Camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
