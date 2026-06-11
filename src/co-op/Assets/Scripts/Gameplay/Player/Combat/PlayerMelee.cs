using FishNet.Object;
using Gameplay.Net;
using Gameplay.Player.Animation;
using Gameplay.Player.Vitals;
using Gameplay.World.Enemies;
using Infrastructure.Services.Input;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Combat
{
    public sealed class PlayerMelee : NetworkBehaviour, IRuntimeInjectionListener
    {
        [SerializeField] private float _range = 2.2f;
        [SerializeField] private float _radius = 1.2f;
        [SerializeField] private float _damage = 15f;
        [SerializeField] private float _cooldown = 0.6f;

        [Inject] private IInputService _input;
        [Inject] private SignalBus _signalBus;
        private PlayerVitals _vitals;
        private PlayerAnimator _animator;
        private bool _mounted;
        private bool _bound;
        private float _cd;
        private float _promptCheck;
        private bool _promptShown;

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            _animator = GetComponent<PlayerAnimator>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            BindOwner();
        }

        public void OnRuntimeInjected() => BindOwner();

        private void BindOwner()
        {
            if (_bound || !IsOwner || _input == null || _signalBus == null) return;
            _input.MeleeStarted += OnMelee;
            _input.FireStarted += OnMelee;
            _signalBus.Subscribe<WeaponMountedSignal>(OnMounted);
            _bound = true;
        }

        public override void OnStopClient()
        {
            if (_bound)
            {
                if (_input != null)
                {
                    _input.MeleeStarted -= OnMelee;
                    _input.FireStarted -= OnMelee;
                }
                _signalBus?.TryUnsubscribe<WeaponMountedSignal>(OnMounted);
                _bound = false;
            }
            base.OnStopClient();
        }

        private void OnMounted(WeaponMountedSignal s) => _mounted = s.Mounted;

        private void Update()
        {
            if (_cd > 0f) _cd -= Time.deltaTime;
            if (!IsOwner) return;

            _promptCheck -= Time.deltaTime;
            if (_promptCheck > 0f) return;
            _promptCheck = 0.15f;

            bool inRange = (_vitals == null || _vitals.IsAlive) && HasTargetInRange();
            if (inRange == _promptShown) return;
            _promptShown = inRange;
            _signalBus?.Fire(new MeleePromptSignal(inRange));
        }

        private bool HasTargetInRange()
        {
            Vector3 center = transform.position + transform.forward * (_range * 0.5f);
            float rSq = _radius * _radius;
            var all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e != null && (e.HitCenter - center).sqrMagnitude <= rSq) return true;
            }
            return false;
        }

        private void OnMelee()
        {
            if (!IsOwner || _cd > 0f || _mounted) return;
            if (_vitals != null && !_vitals.IsAlive) return;
            _cd = _cooldown;
            ServerBash(transform.position, transform.forward);
        }

        [ServerRpc]
        private void ServerBash(Vector3 origin, Vector3 forward)
        {
            Vector3 center = origin + forward * (_range * 0.5f);
            float rSq = _radius * _radius;
            bool any = false;
            var all = Enemy.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var e = all[i];
                if (e == null || (e.HitCenter - center).sqrMagnitude > rSq) continue;
                e.ServerApplyDamage(_damage);
                any = true;
            }
            RpcMelee(center, any);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcMelee(Vector3 pos, bool hit)
        {
            _animator?.TriggerKick();
            _signalBus?.Fire(new PlayerMeleeSignal(pos, hit));
        }
    }
}
