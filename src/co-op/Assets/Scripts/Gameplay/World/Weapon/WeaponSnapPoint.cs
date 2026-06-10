using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Gameplay.World.Items;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Weapon
{
    public sealed class WeaponSnapPoint : InjectableNetworkBehaviour
    {
        [Header("Distances")]
        [Tooltip("From a carried item to this point — within this range the glow particles light up.")]
        [Min(0.05f)] public float HighlightDistance = 2f;

        [Tooltip("On release within this distance, the carried item snaps to this socket.")]
        [Min(0.05f)] public float SnapDistance = 0.5f;

        [Header("Grip")]
        [Tooltip("Total mob-sitting seconds this module withstands before it pops off. Drains by the number of mobs latched on it per second.")]
        [SerializeField] private float _gripBudgetSec = 30f;

        [Header("Visual")]
        [Tooltip("Particle system playing while a carried item is in range. Auto-found among children if null.")]
        [SerializeField] private ParticleSystem _glowParticles;

        [Inject] private SignalBus _signalBus;

        public readonly SyncVar<bool> IsOccupied = new(false);
        public readonly SyncVar<int> MobCount = new(0);

        [System.NonSerialized] public Carryable AttachedCarryable;

        public bool IsFree => !IsOccupied.Value;

        private static readonly List<WeaponSnapPoint> _all = new();
        public static IReadOnlyList<WeaponSnapPoint> All => _all;

        private bool _highlightedLocally;
        private float _grip;

        private void Awake()
        {
            if (_glowParticles == null) _glowParticles = GetComponentInChildren<ParticleSystem>(true);
            if (_glowParticles != null)
            {
                _glowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var em = _glowParticles.emission;
                em.enabled = false;
            }
            _grip = _gripBudgetSec;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            IsOccupied.OnChange += OnOccupiedChanged;
            MobCount.OnChange += OnMobCountChanged;
        }

        public override void OnStopNetwork()
        {
            IsOccupied.OnChange -= OnOccupiedChanged;
            MobCount.OnChange -= OnMobCountChanged;
            base.OnStopNetwork();
        }

        private void OnOccupiedChanged(bool prev, bool next, bool asServer)
        {
            if (IsServerInitialized && next && !prev) { _grip = _gripBudgetSec; MobCount.Value = 0; }
            if (next && !prev) _signalBus?.Fire(new ItemSnappedSignal(transform.position));
            else if (!next && prev) _signalBus?.Fire(new ModuleDetachedSignal(transform.position));
            FireModulesChanged();
        }

        private void OnMobCountChanged(int prev, int next, bool asServer) => FireModulesChanged();

        public void AddMob() { if (IsServerInitialized) MobCount.Value++; }
        public void RemoveMob() { if (IsServerInitialized) MobCount.Value = Mathf.Max(0, MobCount.Value - 1); }

        private void Update()
        {
            if (!IsServerInitialized || !IsOccupied.Value || MobCount.Value <= 0) return;
            _grip -= MobCount.Value * Time.deltaTime;
            if (_grip <= 0f) ServerEject();
        }

        private void FireModulesChanged()
        {
            int total = _all.Count, occupied = 0, underAttack = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                var m = _all[i];
                if (m == null) continue;
                if (m.IsOccupied.Value) occupied++;
                if (m.MobCount.Value > 0) underAttack++;
            }
            _signalBus?.Fire(new CannonModulesChangedSignal(underAttack, total - occupied, total));
        }

        public void ServerEject()
        {
            if (!IsServerInitialized) return;
            var c = AttachedCarryable;
            AttachedCarryable = null;
            MobCount.Value = 0;
            _grip = _gripBudgetSec;
            IsOccupied.Value = false;
            if (c != null)
            {
                c.IsSnapped.Value = false;
                c.ApplyPhysicsState();
                if (c.Body != null && !c.Body.isKinematic)
                    c.Body.AddForce(Vector3.up * 1.5f + transform.forward, ForceMode.VelocityChange);
            }
        }

        public void SetHighlight(bool on)
        {
            if (_highlightedLocally == on) return;
            _highlightedLocally = on;
            if (_glowParticles == null) return;
            var em = _glowParticles.emission;
            em.enabled = on;
            if (on && !_glowParticles.isPlaying) _glowParticles.Play();
            else if (!on && _glowParticles.isPlaying) _glowParticles.Stop();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 1f, 0.8f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, HighlightDistance);
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.95f);
            Gizmos.DrawWireSphere(transform.position, SnapDistance);
            Gizmos.color = new Color(1f, 1f, 0.4f, 1f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        }
#endif
    }
}
