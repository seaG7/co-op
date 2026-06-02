using FishNet.Object;
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

        [Inject] private ICameraService _cameraService;

        private bool _active;

        public UnityEngine.Camera Camera => _camera;

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

            _active = true;
        }

        public override void OnStopClient()
        {
            if (base.IsOwner)
            {
                _active = false;
                if (_camera != null)
                    _camera.transform.SetParent(null, worldPositionStays: true);
            }
            base.OnStopClient();
        }
    }
}
