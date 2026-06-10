using System;
using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Gameplay.World.Items;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Weapon
{
    public sealed class WeaponModuleSlot : InjectableNetworkBehaviour
    {
        private enum GhostMode { Hidden, Next, Pending }

        [Header("Order")]
        [Tooltip("Assembly order (1..N). Modules must be attached in ascending order; only the lowest empty slot accepts its matching module.")]
        [Min(1)] [SerializeField] private int _order = 1;

        [Header("Attach")]
        [Tooltip("How close (m) the carried module must be to this slot for a release to attach it.")]
        [Min(0.05f)] [SerializeField] private float _attachDistance = 0.8f;

        [Header("Grip")]
        [Tooltip("Total mob-sitting seconds this module withstands before it pops off. Drains by the number of mobs latched per second.")]
        [SerializeField] private float _gripBudgetSec = 30f;

        [Header("Ghost shader")]
        [Tooltip("Renderers showing the transparent preview of this module. Empty = every renderer under this slot.")]
        [SerializeField] private Renderer[] _ghostRenderers;
        [Tooltip("Tint applied to the ghost; its alpha is overridden per state.")]
        [SerializeField] private Color _ghostColor = new Color(0.6f, 0.85f, 1f, 1f);
        [Tooltip("Color property on the ghost shader whose alpha is driven (URP Lit/Unlit = _BaseColor).")]
        [SerializeField] private string _colorProperty = "_BaseColor";
        [Tooltip("Optional float opacity property on the ghost shader, set alongside the color alpha.")]
        [SerializeField] private string _alphaProperty = "_Alpha";
        [Tooltip("Alpha for the NEXT module to assemble (more visible — the hint).")]
        [Range(0f, 1f)] [SerializeField] private float _nextAlpha = 0.55f;
        [Tooltip("Alpha for the other not-yet-assembled modules (faint).")]
        [Range(0f, 1f)] [SerializeField] private float _pendingAlpha = 0.12f;

        [Inject] private SignalBus _signalBus;

        public readonly SyncVar<bool> IsOccupied = new(false);
        public readonly SyncVar<int> MobCount = new(0);

        [NonSerialized] public Carryable AttachedModule;

        public int Order => _order;
        public bool IsFree => !IsOccupied.Value;
        public float AttachDistance => _attachDistance;

        private static readonly List<WeaponModuleSlot> _all = new();
        public static IReadOnlyList<WeaponModuleSlot> All => _all;

        public static int NextOrder()
        {
            int best = int.MaxValue;
            for (int i = 0; i < _all.Count; i++)
            {
                var s = _all[i];
                if (s != null && !s.IsOccupied.Value && s._order < best) best = s._order;
            }
            return best;
        }

        public static WeaponModuleSlot Find(int order)
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i]._order == order) return _all[i];
            return null;
        }

        private float _grip;
        private MaterialPropertyBlock _mpb;
        private int _colorId;
        private int _alphaId;

        private void Awake()
        {
            if (_ghostRenderers == null || _ghostRenderers.Length == 0)
                _ghostRenderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            _colorId = Shader.PropertyToID(string.IsNullOrEmpty(_colorProperty) ? "_BaseColor" : _colorProperty);
            _alphaId = Shader.PropertyToID(string.IsNullOrEmpty(_alphaProperty) ? "_Alpha" : _alphaProperty);
            _grip = _gripBudgetSec;
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            IsOccupied.OnChange += OnOccupiedChanged;
            MobCount.OnChange += OnMobCountChanged;
            RefreshAllGhosts();
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
            else if (!next && prev) _signalBus?.Fire(new ModuleDetachedSignal(transform.position, _order));
            FireModulesChanged();
            RefreshAllGhosts();
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
            var c = AttachedModule;
            AttachedModule = null;
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

        private static void RefreshAllGhosts()
        {
            int next = NextOrder();
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null) _all[i].ApplyGhost(next);
        }

        private void ApplyGhost(int nextOrder)
        {
            if (_ghostRenderers == null || _mpb == null) return;
            GhostMode mode = IsOccupied.Value ? GhostMode.Hidden : (_order == nextOrder ? GhostMode.Next : GhostMode.Pending);
            bool show = mode != GhostMode.Hidden;
            float alpha = mode == GhostMode.Next ? _nextAlpha : _pendingAlpha;
            Color c = _ghostColor; c.a = alpha;
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                var r = _ghostRenderers[i];
                if (r == null) continue;
                r.enabled = show;
                if (!show) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(_colorId, c);
                _mpb.SetFloat(_alphaId, alpha);
                r.SetPropertyBlock(_mpb);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, _attachDistance);
            Gizmos.color = new Color(1f, 1f, 0.4f, 1f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
        }
#endif
    }
}
