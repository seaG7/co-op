using System;
using Data.Effects;
using Gameplay.Player.Vitals;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Effects
{
    public sealed class PlayerEffectBindings : IInitializable, IDisposable
    {
        private readonly SignalBus _bus;
        private readonly IVfxService _vfx;
        private readonly ISfxService _sfx;

        public PlayerEffectBindings(SignalBus bus, IVfxService vfx, ISfxService sfx)
        {
            _bus = bus;
            _vfx = vfx;
            _sfx = sfx;
        }

        public void Initialize()
        {
            _bus.Subscribe<PlayerDownedSignal>(OnDowned);
            _bus.Subscribe<PlayerRevivedSignal>(OnRevived);
            _bus.Subscribe<PlayerDiedSignal>(OnDied);
            _bus.Subscribe<PlayerMeleeSignal>(OnMelee);
        }

        public void Dispose()
        {
            _bus.Unsubscribe<PlayerDownedSignal>(OnDowned);
            _bus.Unsubscribe<PlayerRevivedSignal>(OnRevived);
            _bus.Unsubscribe<PlayerDiedSignal>(OnDied);
            _bus.Unsubscribe<PlayerMeleeSignal>(OnMelee);
        }

        private static Vector3 PlayerPos(int clientId)
        {
            var all = PlayerVitals.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].OwnerId == clientId) return all[i].transform.position;
            return Vector3.zero;
        }

        private void OnDowned(PlayerDownedSignal s) { Vector3 p = PlayerPos(s.ClientId); _vfx.Play(VfxId.PlayerKnockdown, p); _sfx.Play(SfxId.PlayerKnockdown, p); }
        private void OnRevived(PlayerRevivedSignal s) { Vector3 p = PlayerPos(s.ClientId); _vfx.Play(VfxId.ReviveDone, p); _sfx.Play(SfxId.ReviveDone, p); }
        private void OnDied(PlayerDiedSignal s) { Vector3 p = PlayerPos(s.ClientId); _vfx.Play(VfxId.PlayerDeath, p); _sfx.Play(SfxId.PlayerDeath, p); }

        private void OnMelee(PlayerMeleeSignal s)
        {
            _vfx.Play(VfxId.MeleeSwing, s.Position);
            _sfx.Play(SfxId.MeleeSwing, s.Position);
            if (s.Hit)
            {
                _vfx.Play(VfxId.MeleeHit, s.Position);
                _sfx.Play(SfxId.MeleeHit, s.Position);
            }
        }
    }
}
