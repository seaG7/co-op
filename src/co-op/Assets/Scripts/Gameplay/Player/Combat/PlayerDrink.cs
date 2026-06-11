using FishNet.Object;
using Gameplay.Net;
using Gameplay.Player.Animation;
using Gameplay.Player.Camera;
using Gameplay.Player.Movement;
using Gameplay.Player.Vitals;
using Gameplay.World.Items;
using Infrastructure.Services.Input;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Combat
{
    // Hold E to drink a Drinkable bottle: hand raises the bottle (Drinking clip), player stands
    // still. Release E early -> cancel (bottle drops, no effect). Hold to the end -> bottle is
    // tossed back into the world and the player gets (stacking) drunk. Server-authoritative;
    // the bottle is pinned to the drink anchor on every client for the duration.
    public sealed class PlayerDrink : NetworkBehaviour, IRuntimeInjectionListener
    {
        [SerializeField] private LayerMask _drinkableMask;
        [SerializeField] private Transform _drinkAnchor;
        [SerializeField] private float _reach = 2.5f;
        [SerializeField] private float _drinkDuration = 3f;
        [SerializeField] private float _throwForce = 2.5f;

        [Inject] private IInputService _input;

        public Transform DrinkAnchor => _drinkAnchor;

        private PlayerCameraRig _cameraRig;
        private PlayerAnimator _animator;
        private PlayerMovement _movement;
        private PlayerDrunk _drunk;
        private PlayerVitals _vitals;

        private bool _inputBound;
        private bool _drinking;     // server
        private float _timer;       // server
        private Drinkable _bottle;  // server

        private void Awake()
        {
            _cameraRig = GetComponent<PlayerCameraRig>();
            _animator = GetComponent<PlayerAnimator>();
            _movement = GetComponent<PlayerMovement>();
            _drunk = GetComponent<PlayerDrunk>();
            _vitals = GetComponent<PlayerVitals>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            BindInput();
        }

        public void OnRuntimeInjected() => BindInput();

        private void BindInput()
        {
            if (_inputBound || !base.IsOwner || _input == null) return;
            _input.InteractStarted += OnInteractStarted;
            _input.InteractCanceled += OnInteractCanceled;
            _inputBound = true;
        }

        public override void OnStopClient()
        {
            if (_inputBound && _input != null)
            {
                _input.InteractStarted -= OnInteractStarted;
                _input.InteractCanceled -= OnInteractCanceled;
                _inputBound = false;
            }
            base.OnStopClient();
        }

        public override void OnStopServer()
        {
            if (_drinking) ServerEndDrink(false);
            base.OnStopServer();
        }

        private void OnInteractStarted()
        {
            if (_vitals != null && !_vitals.IsAlive) return;
            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null) return;
            const float probe = 50f;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit,
                    probe, _drinkableMask, QueryTriggerInteraction.Ignore))
                return;
            if (Vector3.Distance(transform.position, hit.point) > _reach) return;
            var drinkable = hit.collider.GetComponentInParent<Drinkable>();
            if (drinkable == null || drinkable.IsClaimed) return;
            RequestDrink(drinkable.NetworkObject);
        }

        private void OnInteractCanceled() => RequestCancel();

        [ServerRpc]
        private void RequestDrink(NetworkObject bottleNob)
        {
            if (_drinking || bottleNob == null) return;
            var d = bottleNob.GetComponent<Drinkable>();
            if (d == null || d.IsClaimed) return;
            if (Vector3.Distance(transform.position, d.transform.position) > _reach * 1.5f) return;

            d.DrinkerClientId.Value = base.OwnerId;
            _bottle = d;
            _drinking = true;
            _timer = _drinkDuration;
            RpcDrinking(true);
        }

        [ServerRpc]
        private void RequestCancel()
        {
            if (_drinking) ServerEndDrink(false);
        }

        private void Update()
        {
            if (!base.IsServerInitialized || !_drinking) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f) ServerEndDrink(true);
        }

        private void ServerEndDrink(bool complete)
        {
            _drinking = false;
            var b = _bottle;
            _bottle = null;
            if (b != null)
            {
                if (complete) b.ServerToss(transform.forward * _throwForce + Vector3.up * 0.5f);
                else b.DrinkerClientId.Value = -1;
            }
            if (complete && _drunk != null) _drunk.ServerAddDrink();
            RpcDrinking(false);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcDrinking(bool drinking)
        {
            _animator?.SetDrinking(drinking);
            if (base.IsOwner && _movement != null) _movement.enabled = !drinking;
        }

        private void LateUpdate()
        {
            if (_drinkAnchor == null) return;
            var all = Drinkable.All;
            int myId = base.OwnerId;
            for (int i = 0; i < all.Count; i++)
            {
                var d = all[i];
                if (d != null && d.DrinkerClientId.Value == myId)
                {
                    d.transform.SetPositionAndRotation(_drinkAnchor.position, _drinkAnchor.rotation);
                    return;
                }
            }
        }
    }
}
