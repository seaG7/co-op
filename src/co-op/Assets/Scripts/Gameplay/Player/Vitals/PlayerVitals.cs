using System.Collections.Generic;
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
        private Renderer[] _renderers;

        private Transform _spectateTarget;
        private float _downTimer;

        public PlayerLifeState State => _state.Value;
        public bool IsAlive => _state.Value == PlayerLifeState.Alive;
        public bool IsDowned => _state.Value == PlayerLifeState.Downed;
        public bool IsDead => _state.Value == PlayerLifeState.Dead;
        public bool HasLatchedAttacker { get; set; }

        private void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _carry = GetComponent<PlayerCarry>();
            _look = GetComponent<PlayerLookController>();
            _cameraRig = GetComponent<PlayerCameraRig>();
            _cc = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>(true);
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
            HasLatchedAttacker = true;
            _downTimer = _configs?.Vitals != null ? Mathf.Max(0.1f, _configs.Vitals.DownReviveSeconds) : 15f;
            _state.Value = PlayerLifeState.Downed;
            if (_carry != null) _carry.ServerForceDrop();
            if (NoneAlive()) _signalBus?.Fire(new AllPlayersDownedOrDeadSignal());
        }

        public void ServerRevive()
        {
            if (!base.IsServerInitialized || _state.Value != PlayerLifeState.Downed) return;
            HasLatchedAttacker = false;
            _state.Value = PlayerLifeState.Alive;
        }

        private void Update()
        {
            if (!base.IsServerInitialized || _state.Value != PlayerLifeState.Downed) return;
            _downTimer -= Time.deltaTime;
            if (_downTimer <= 0f) ServerDie();
        }

        private void ServerDie()
        {
            if (!base.IsServerInitialized || _state.Value != PlayerLifeState.Downed) return;
            HasLatchedAttacker = false;
            _state.Value = PlayerLifeState.Dead;
            if (NoneAlive()) _signalBus?.Fire(new AllPlayersDownedOrDeadSignal());
        }

        private static bool NoneAlive()
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].State == PlayerLifeState.Alive) return false;
            return true;
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
            bool downed = state == PlayerLifeState.Downed;
            bool dead = state == PlayerLifeState.Dead;

            if (base.IsOwner)
            {
                if (_movement != null) _movement.enabled = alive;
                SetOwnerBodyVisible(downed);

                if (dead)
                {
                    if (_look != null) _look.enabled = false;
                    BeginSpectate();
                }
                else if (downed)
                {
                    if (_look != null) _look.enabled = false;
                    if (_cameraRig != null) _cameraRig.SetDownedView(true);
                }
                else
                {
                    StopSpectate();
                    if (_cameraRig != null) _cameraRig.SetDownedView(false);
                }
            }

            if (_cc != null) _cc.enabled = !dead;
        }

        private void SetOwnerBodyVisible(bool visible)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = visible;
        }

        private void BeginSpectate()
        {
            _spectateTarget = FindAliveTeammate();
            var off = _configs?.Vitals != null ? _configs.Vitals.SpectateCameraOffset : new Vector3(0f, 2f, -3.5f);
            if (_cameraRig != null) _cameraRig.SpectateFollow(_spectateTarget, off);
        }

        private void StopSpectate()
        {
            if (_spectateTarget == null) return;
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
    }
}
