using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Camera
{
    public sealed class CameraService : ICameraService
    {
        private readonly UnityEngine.Camera _explicitCamera;

        public CameraService([InjectOptional] UnityEngine.Camera camera) => _explicitCamera = camera;

        public UnityEngine.Camera ResolveCamera() =>
            _explicitCamera != null ? _explicitCamera : UnityEngine.Camera.main;
    }
}
