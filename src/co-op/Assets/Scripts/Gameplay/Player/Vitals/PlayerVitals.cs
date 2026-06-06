using System.Collections.Generic;
using Data.Configs;
using Data.Players;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Gameplay.Player.Camera;
using Gameplay.Player.Carry;
using Gameplay.Player.Look;
using Gameplay.Player.Movement;
using Infrastructure.Providers.Configs;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Vitals
{
    public sealed class PlayerVitals : NetworkBehaviour
    {
        private static readonly List<PlayerVitals> _all = new();
        public static IReadOnlyList<PlayerVitals> All => _all;

        [Inject] private IConfigDataProvider _configs;
        [Inject] private SignalBus _signalBus;

        private readonly SyncVar<PlayerLifeState> _state = new(PlayerLifeState.Alive);

        private PlayerMovement _movement;
        private PlayerCarry _carry;
        private PlayerLookController _look;
        private PlayerCameraRig _cameraRig;
        private CharacterController _cc;

        private float _downTimer;
        private float _reviveProgress;
        private float _progressBroadcastTimer;
        private Transform _spectateTarget;

        public PlayerLifeState State => _state.Value;
        public bool IsAlive => _state.Value == PlayerLifeState.Alive;
        public bool IsDowned => _state.Value == PlayerLifeState.Downed;
        public bool IsDead => _state.Value == PlayerLifeState.Dead;

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _carry = GetComponent<PlayerCarry>();
            _look = GetComponent<PlayerLookController>();
            _cameraRig = GetComponent<PlayerCameraRig>();
            _cc = GetComponent<CharacterController>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
            _state.OnChange += OnStateChanged;
        }

        public override void OnStopNetwork()
        {
            _state.OnChange -= OnStateChanged;
            _all.Remove(this);
            base.OnStopNetwork();
        }

        public void ServerKnockDown()
        {
            if (!base.IsServerInitialized || _state.Value != PlayerLifeState.Alive) return;
            var cfg = _configs?.Vitals;
            _downTimer = cfg != null ? cfg.DownReviveSeconds : 15f;
            _reviveProgress = 0f;
            _progressBroadcastTimer = 0f;
            _state.Value = PlayerLifeState.Downed;
            if (_carry != null) _carry.ServerForceDrop();
        }

        private void Update()
        {
            if (!base.IsServerInitialized || _state.Value != PlayerLifeState.Downed) return;

            var cfg = _configs?.Vitals;
            float hold = cfg != null ? cfg.ReviveHoldSeconds : 3f;
            float dt = Time.deltaTime;

            if (HasAliveReviverInRange(cfg))
            {
                _reviveProgress += dt;
                if (hold > 0f && _reviveProgress >= hold) { ServerRevive(); return; }
            }
            else
            {
                float decay = cfg != null ? cfg.ReviveDecayMultiplier : 2f;
                _reviveProgress = Mathf.Max(0f, _reviveProgress - dt * decay);
                _downTimer -= dt;
                if (_downTimer <= 0f) { ServerDie(); return; }
            }

            _progressBroadcastTimer -= dt;
            if (_progressBroadcastTimer <= 0f)
            {
                _progressBroadcastTimer = 0.2f;
                BroadcastProgress(Mathf.Max(0f, _downTimer), hold > 0f ? Mathf.Clamp01(_reviveProgress / hold) : 0f);
            }
        }

        private bool HasAliveReviverInRange(VitalsConfig cfg)
        {
            float range = cfg != null ? cfg.ReviveRange : 2.5f;
            float rangeSq = range * range;
            Vector3 p = transform.position;
            for (int i = 0; i < _all.Count; i++)
            {
                var v = _all[i];
                if (v == null || v == this || v.State != PlayerLifeState.Alive) continue;
                if ((v.transform.position - p).sqrMagnitude <= rangeSq) return true;
            }
            return false;
        }

        private void ServerRevive()
        {
            _reviveProgress = 0f;
            _downTimer = 0f;
            _state.Value = PlayerLifeState.Alive;
        }

        private void ServerDie()
        {
            _state.Value = PlayerLifeState.Dead;
            if (NoneAlive())
                _signalBus?.Fire(new AllPlayersDownedOrDeadSignal());
        }

        private static bool NoneAlive()
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].State == PlayerLifeState.Alive) return false;
            return true;
        }

        [ObserversRpc]
        private void BroadcastProgress(float remaining, float reviveProgress01)
        {
            _signalBus?.Fire(new DownStateProgressSignal(base.OwnerId, base.IsOwner, remaining, reviveProgress01));
        }

        private void OnStateChanged(PlayerLifeState prev, PlayerLifeState next, bool asServer)
        {
            if (asServer) return;

            ApplyLifeStateLocally(next);

            switch (next)
            {
                case PlayerLifeState.Downed:
                    _signalBus?.Fire(new PlayerDownedSignal(base.OwnerId, base.IsOwner));
                    break;
                case PlayerLifeState.Alive:
                    if (prev == PlayerLifeState.Downed)
                        _signalBus?.Fire(new PlayerRevivedSignal(base.OwnerId, base.IsOwner));
                    break;
                case PlayerLifeState.Dead:
                    _signalBus?.Fire(new PlayerDiedSignal(base.OwnerId, base.IsOwner));
                    break;
            }
        }

        private void ApplyLifeStateLocally(PlayerLifeState state)
        {
            bool alive = state == PlayerLifeState.Alive;
            bool dead = state == PlayerLifeState.Dead;

            if (base.IsOwner)
            {
                if (_movement != null) _movement.enabled = alive;
                if (_look != null) _look.enabled = !dead;
                if (dead) BeginSpectate();
                else StopSpectate();
            }

            if (_cc != null) _cc.enabled = !dead;
        }

        private void BeginSpectate()
        {
            _spectateTarget = FindAliveTeammate();
            var off = _configs?.Vitals != null ? _configs.Vitals.SpectateCameraOffset : new Vector3(0f, 0.85f, 0f);
            if (_cameraRig != null) _cameraRig.SpectateFollow(_spectateTarget, off);
        }

        private void StopSpectate()
        {
            _spectateTarget = null;
            if (_cameraRig != null) _cameraRig.SpectateFollow(null, Vector3.zero);
        }

        private Transform FindAliveTeammate()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var v = _all[i];
                if (v != null && v != this && v.State == PlayerLifeState.Alive) return v.transform;
            }
            return null;
        }

        private void LateUpdate()
        {
            if (!base.IsOwner || _state.Value != PlayerLifeState.Dead) return;

            bool targetGone = _spectateTarget == null;
            if (!targetGone)
            {
                var tv = _spectateTarget.GetComponent<PlayerVitals>();
                targetGone = tv == null || tv.State != PlayerLifeState.Alive;
            }
            if (targetGone) BeginSpectate();
        }
    }
}
