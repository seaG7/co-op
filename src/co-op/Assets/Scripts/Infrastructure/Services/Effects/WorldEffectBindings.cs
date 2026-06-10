using System;
using Data.Effects;
using Data.Rounds;
using Data.World;
using Gameplay.World.Portals;
using Gameplay.World.Sources;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Effects
{
    public sealed class WorldEffectBindings : IInitializable, IDisposable
    {
        private readonly SignalBus _bus;
        private readonly IVfxService _vfx;
        private readonly ISfxService _sfx;

        private IVfxHandle _auraVfx;
        private ISfxHandle _auraSfx;

        public WorldEffectBindings(SignalBus bus, IVfxService vfx, ISfxService sfx)
        {
            _bus = bus;
            _vfx = vfx;
            _sfx = sfx;
        }

        public void Initialize()
        {
            _bus.Subscribe<ItemImpactSignal>(OnItemImpact);
            _bus.Subscribe<ItemSnappedSignal>(OnItemSnapped);
            _bus.Subscribe<WeaponMountedSignal>(OnMounted);
            _bus.Subscribe<WeaponFiredSignal>(OnFired);
            _bus.Subscribe<HarpoonImpactSignal>(OnHarpoonImpact);
            _bus.Subscribe<SourceStateChangedSignal>(OnSourceState);
            _bus.Subscribe<SourceVulnerableSignal>(OnVulnerable);
            _bus.Subscribe<SourceDamagedSignal>(OnSourceDamaged);
            _bus.Subscribe<SourceDestroyedSignal>(OnSourceDestroyed);
            _bus.Subscribe<PortalEnteredSignal>(OnPortalEntered);
            _bus.Subscribe<WaveStartedSignal>(OnWaveStarted);
            _bus.Subscribe<GameEndedSignal>(OnGameEnded);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<ItemImpactSignal>(OnItemImpact);
            _bus.Unsubscribe<ItemSnappedSignal>(OnItemSnapped);
            _bus.Unsubscribe<WeaponMountedSignal>(OnMounted);
            _bus.Unsubscribe<WeaponFiredSignal>(OnFired);
            _bus.Unsubscribe<HarpoonImpactSignal>(OnHarpoonImpact);
            _bus.Unsubscribe<SourceStateChangedSignal>(OnSourceState);
            _bus.Unsubscribe<SourceVulnerableSignal>(OnVulnerable);
            _bus.Unsubscribe<SourceDamagedSignal>(OnSourceDamaged);
            _bus.Unsubscribe<SourceDestroyedSignal>(OnSourceDestroyed);
            _bus.Unsubscribe<PortalEnteredSignal>(OnPortalEntered);
            _bus.Unsubscribe<WaveStartedSignal>(OnWaveStarted);
            _bus.Unsubscribe<GameEndedSignal>(OnGameEnded);
            _auraVfx?.Stop();
            _auraSfx?.Stop();
        }

        private static Vector3 SourcePos()
            => Source.All.Count > 0 && Source.All[0] != null ? Source.All[0].transform.position : Vector3.zero;

        private static Vector3 PortalPos()
            => Portal.All.Count > 0 && Portal.All[0] != null ? Portal.All[0].transform.position : Vector3.zero;

        private void OnItemImpact(ItemImpactSignal s) { _vfx.Play(VfxId.ItemImpact, s.Point); _sfx.Play(SfxId.ItemDrop, s.Point); }
        private void OnItemSnapped(ItemSnappedSignal s) { _vfx.Play(VfxId.SnapConfirm, s.Position); _sfx.Play(SfxId.ItemSnap, s.Position); }
        private void OnMounted(WeaponMountedSignal s) { if (s.Mounted) _sfx.Play(SfxId.WeaponMount, SourcePos()); }

        private void OnFired(WeaponFiredSignal s)
        {
            _vfx.Play(VfxId.MuzzleFlash, s.Origin);
            _sfx.Play(SfxId.WeaponFire, s.Origin);
            if (s.Hit)
            {
                _vfx.Play(VfxId.ShotImpact, s.HitPoint);
                _sfx.Play(SfxId.ShotImpact, s.HitPoint);
            }
        }

        private void OnHarpoonImpact(HarpoonImpactSignal s)
        {
            _vfx.Play(VfxId.HarpoonImpact, s.Point);
            _sfx.Play(SfxId.ShotImpact, s.Point);
        }

        private void OnSourceState(SourceStateChangedSignal s)
        {
            if (s.State != SourceState.Open) return;
            Vector3 p = SourcePos();
            _vfx.Play(VfxId.SourceOpen, p);
            _sfx.Play(SfxId.SourceOpen, p);
            if (_auraVfx == null && Source.All.Count > 0 && Source.All[0] != null)
            {
                _auraVfx = _vfx.PlayLoop(VfxId.SourceAura, Source.All[0].transform);
                _auraSfx = _sfx.PlayLoop(SfxId.SourceAmbient, Source.All[0].transform);
            }
        }

        private void OnVulnerable(SourceVulnerableSignal s)
        {
            if (!s.Vulnerable) return;
            Vector3 p = SourcePos();
            _vfx.Play(VfxId.SourceVulnerable, p);
            _sfx.Play(SfxId.SourceShootNow, p);
        }

        private void OnSourceDamaged(SourceDamagedSignal s) { Vector3 p = SourcePos(); _vfx.Play(VfxId.SourceHit, p); _sfx.Play(SfxId.SourceHit, p); }

        private void OnSourceDestroyed(SourceDestroyedSignal s)
        {
            Vector3 p = SourcePos();
            _vfx.Play(VfxId.SourceExplode, p);
            _sfx.Play(SfxId.SourceExplode, p);
            _auraVfx?.Stop(); _auraVfx = null;
            _auraSfx?.Stop(); _auraSfx = null;
            _vfx.Play(VfxId.PortalAppear, p);
            _sfx.Play(SfxId.PortalAppear, p);
        }

        private void OnPortalEntered(PortalEnteredSignal s) { Vector3 p = PortalPos(); _vfx.Play(VfxId.PortalIdle, p); _sfx.Play(SfxId.PortalEnter, p); }
        private void OnWaveStarted(WaveStartedSignal s) => _sfx.Play2D(SfxId.WaveStart);

        private void OnGameEnded(GameEndedSignal s)
        {
            if (s.Outcome == RoundOutcome.Victory) _sfx.Play2D(SfxId.Victory);
            else if (s.Outcome == RoundOutcome.Defeat) _sfx.Play2D(SfxId.Defeat);
        }
    }
}
