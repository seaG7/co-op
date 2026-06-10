using FishNet.Object;
using Gameplay.Player.Vitals;
using Gameplay.World.Enemies;
using Infrastructure.Services.Input;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Combat
{
    public sealed class PlayerMelee : NetworkBehaviour
    {
        [SerializeField] private float _range = 2.2f;
        [SerializeField] private float _radius = 1.2f;
        [SerializeField] private float _damage = 15f;
        [SerializeField] private float _cooldown = 0.6f;

        [Inject] private IInputService _input;
        [Inject] private SignalBus _signalBus;
        private PlayerVitals _vitals;
        private float _cd;
        private float _promptCheck;
        private bool _promptShown;

        private void Awake() => _vitals = GetComponent<PlayerVitals>();

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner && _input != null) _input.MeleeStarted += OnMelee;
        }

        public override void OnStopClient()
        {
            if (IsOwner && _input != null) _input.MeleeStarted -= OnMelee;
            base.OnStopClient();
        }

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
            var hits = Physics.OverlapSphere(center, _radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
                if (hits[i].GetComponentInParent<Enemy>() != null) return true;
            return false;
        }

        private void OnMelee()
        {
            if (!IsOwner || _cd > 0f) return;
            if (_vitals != null && !_vitals.IsAlive) return;
            _cd = _cooldown;
            ServerBash(transform.position, transform.forward);
        }

        [ServerRpc]
        private void ServerBash(Vector3 origin, Vector3 forward)
        {
            Vector3 center = origin + forward * (_range * 0.5f);
            var hits = Physics.OverlapSphere(center, _radius, ~0, QueryTriggerInteraction.Ignore);
            bool any = false;
            for (int i = 0; i < hits.Length; i++)
            {
                var enemy = hits[i].GetComponentInParent<Enemy>();
                if (enemy != null) { enemy.ServerApplyDamage(_damage); any = true; }
            }
            RpcMelee(center, any);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcMelee(Vector3 pos, bool hit) => _signalBus?.Fire(new PlayerMeleeSignal(pos, hit));
    }
}
