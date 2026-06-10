using System;
using Data.Effects;
using Signals;
using Zenject;

namespace Infrastructure.Services.Effects
{
    public sealed class EnemyEffectBindings : IInitializable, IDisposable
    {
        private readonly SignalBus _bus;
        private readonly IVfxService _vfx;
        private readonly ISfxService _sfx;

        public EnemyEffectBindings(SignalBus bus, IVfxService vfx, ISfxService sfx)
        {
            _bus = bus;
            _vfx = vfx;
            _sfx = sfx;
        }

        public void Initialize()
        {
            _bus.Subscribe<EnemySpawnedSignal>(OnSpawn);
            _bus.Subscribe<EnemyPrePounceSignal>(OnPrePounce);
            _bus.Subscribe<EnemyPouncedSignal>(OnPounce);
            _bus.Subscribe<EnemyLatchedSignal>(OnLatch);
            _bus.Subscribe<EnemyDamagedSignal>(OnDamaged);
            _bus.Subscribe<EnemyDiedSignal>(OnDied);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<EnemySpawnedSignal>(OnSpawn);
            _bus.Unsubscribe<EnemyPrePounceSignal>(OnPrePounce);
            _bus.Unsubscribe<EnemyPouncedSignal>(OnPounce);
            _bus.Unsubscribe<EnemyLatchedSignal>(OnLatch);
            _bus.Unsubscribe<EnemyDamagedSignal>(OnDamaged);
            _bus.Unsubscribe<EnemyDiedSignal>(OnDied);
        }

        private void OnSpawn(EnemySpawnedSignal s) { _vfx.Play(VfxId.EnemySpawn, s.Position); _sfx.Play(SfxId.EnemySpawn, s.Position); }
        private void OnPrePounce(EnemyPrePounceSignal s) => _sfx.Play(SfxId.EnemyPrePounce, s.Position);
        private void OnPounce(EnemyPouncedSignal s) { _vfx.Play(VfxId.EnemyPounce, s.Position); _sfx.Play(SfxId.EnemyPounce, s.Position); }
        private void OnLatch(EnemyLatchedSignal s) { _vfx.Play(VfxId.LatchImpact, s.Position); _sfx.Play(SfxId.LatchImpact, s.Position); }
        private void OnDamaged(EnemyDamagedSignal s) { _vfx.Play(VfxId.EnemyHit, s.Position); _sfx.Play(SfxId.EnemyHit, s.Position); }
        private void OnDied(EnemyDiedSignal s) { _vfx.Play(VfxId.EnemyDeath, s.Position); _sfx.Play(SfxId.EnemyDeath, s.Position); }
    }
}
