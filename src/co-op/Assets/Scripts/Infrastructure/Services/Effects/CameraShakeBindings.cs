using System;
using Gameplay.Player.Camera;
using Signals;
using Zenject;

namespace Infrastructure.Services.Effects
{
    public sealed class CameraShakeBindings : IInitializable, IDisposable
    {
        private readonly SignalBus _bus;

        public CameraShakeBindings(SignalBus bus) { _bus = bus; }

        public void Initialize()
        {
            _bus.Subscribe<WeaponFiredSignal>(OnFired);
            _bus.Subscribe<SourceDestroyedSignal>(OnSourceDestroyed);
            _bus.Subscribe<PlayerDownedSignal>(OnDowned);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<WeaponFiredSignal>(OnFired);
            _bus.Unsubscribe<SourceDestroyedSignal>(OnSourceDestroyed);
            _bus.Unsubscribe<PlayerDownedSignal>(OnDowned);
        }

        private void OnFired(WeaponFiredSignal s) => PlayerCameraRig.Local?.AddTrauma(0.25f);
        private void OnSourceDestroyed(SourceDestroyedSignal s) => PlayerCameraRig.Local?.AddTrauma(0.8f);
        private void OnDowned(PlayerDownedSignal s) { if (s.IsLocal) PlayerCameraRig.Local?.AddTrauma(0.6f); }
    }
}
