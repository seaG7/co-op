using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Infrastructure.Services.Spawn;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Weapon
{
    public sealed class WeaponModuleSlot : InjectableNetworkBehaviour
    {
        private enum GhostMode { Solid, Next, Pending }

        [Header("Order")]
        [Tooltip("Assembly order (1..N). Only the lowest not-yet-assembled module accepts its matching carried module.")]
        [Min(1)] [SerializeField] private int _order = 1;

        [Header("Attach")]
        [Tooltip("How close (m) the player + carried module must be to this module's place on the cannon to assemble it (the green gizmo sphere).")]
        [Min(0.1f)] [SerializeField] private float _attachDistance = 1.8f;
        [Tooltip("Optional explicit attach/aim center — drag a child Transform here to place it. If null, the center of this module's meshes is used automatically (NOT the pivot at 0,0,0).")]
        [SerializeField] private Transform _attachPoint;

        [Header("Grip / eject")]
        [Tooltip("Total mob-sitting seconds before the module is torn off. Drains by the number of mobs latched per second.")]
        [SerializeField] private float _gripBudgetSec = 30f;
        [Tooltip("Carryable module prefab dropped near the cannon when mobs tear this module off, so it can be re-collected.")]
        [SerializeField] private GameObject _moduleItemPrefab;

        [Header("Ghost shader")]
        [Tooltip("Transparent ghost material (your imported shader). Assigned: meshes swap to it while previewed and back to their real materials when assembled. Null: just drives alpha on the existing materials.")]
        [SerializeField] private Material _ghostMaterial;
        [Tooltip("Renderers for this module's preview. Empty = all renderers under this object.")]
        [SerializeField] private Renderer[] _ghostRenderers;
        [SerializeField] private Color _ghostColor = new Color(0.6f, 0.85f, 1f, 1f);
        [Tooltip("Color property whose alpha is driven (URP Lit/Unlit = _BaseColor).")]
        [SerializeField] private string _colorProperty = "_BaseColor";
        [Tooltip("Optional float opacity property, set alongside the color alpha.")]
        [SerializeField] private string _alphaProperty = "_Alpha";
        [Tooltip("Alpha for the NEXT module to assemble (the brighter hint).")]
        [Range(0f, 1f)] [SerializeField] private float _nextAlpha = 0.55f;
        [Tooltip("Alpha for the other not-yet-assembled modules (faint).")]
        [Range(0f, 1f)] [SerializeField] private float _pendingAlpha = 0.12f;

        [Inject] private SignalBus _signalBus;
        [Inject] private INetworkSpawnService _spawner;

        public readonly SyncVar<bool> IsOccupied = new(false);
        public readonly SyncVar<int> MobCount = new(0);

        public int Order => _order;
        public bool IsFree => !IsOccupied.Value;
        public float AttachDistance => _attachDistance;

        public Vector3 GhostCenter
        {
            get
            {
                if (_attachPoint != null) return _attachPoint.position;
                var rends = (_ghostRenderers != null && _ghostRenderers.Length > 0)
                    ? _ghostRenderers
                    : GetComponentsInChildren<Renderer>(true);
                Bounds b = default;
                bool has = false;
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i];
                    if (r == null) continue;
                    if (!has) { b = r.bounds; has = true; }
                    else b.Encapsulate(r.bounds);
                }
                return has ? b.center : transform.position;
            }
        }

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
        private Material[][] _originalMats;
        private Material[][] _ghostMats;

        private void Awake()
        {
            if (_ghostRenderers == null || _ghostRenderers.Length == 0)
                _ghostRenderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            _colorId = Shader.PropertyToID(string.IsNullOrEmpty(_colorProperty) ? "_BaseColor" : _colorProperty);
            _alphaId = Shader.PropertyToID(string.IsNullOrEmpty(_alphaProperty) ? "_Alpha" : _alphaProperty);
            _grip = _gripBudgetSec;

            _originalMats = new Material[_ghostRenderers.Length][];
            _ghostMats = new Material[_ghostRenderers.Length][];
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                var r = _ghostRenderers[i];
                if (r == null) continue;
                _originalMats[i] = r.sharedMaterials;
                if (_ghostMaterial != null)
                {
                    var arr = new Material[_originalMats[i].Length];
                    for (int j = 0; j < arr.Length; j++) arr[j] = _ghostMaterial;
                    _ghostMats[i] = arr;
                }
            }
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
            if (next && !prev) _signalBus?.Fire(new ItemSnappedSignal(GhostCenter));
            else if (!next && prev) _signalBus?.Fire(new ModuleDetachedSignal(GhostCenter, _order));
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

        public void ServerEject()
        {
            if (!IsServerInitialized) return;
            MobCount.Value = 0;
            _grip = _gripBudgetSec;
            IsOccupied.Value = false;
            if (_moduleItemPrefab != null && _spawner != null)
                _spawner.SpawnNetworked(_moduleItemPrefab, GhostCenter + Vector3.up * 0.4f, Quaternion.identity, null);
        }

        private static readonly List<CannonModuleState> _stateBuf = new();
        private static readonly System.Comparison<CannonModuleState> _byOrder = (a, b) => a.Order.CompareTo(b.Order);

        private void FireModulesChanged()
        {
            _stateBuf.Clear();
            int occupied = 0, underAttack = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                var m = _all[i];
                if (m == null) continue;
                if (m.IsOccupied.Value) occupied++;
                if (m.MobCount.Value > 0) underAttack++;
                _stateBuf.Add(new CannonModuleState(m._order, m.IsOccupied.Value, m.MobCount.Value));
            }
            _stateBuf.Sort(_byOrder);
            int total = _stateBuf.Count;
            _signalBus?.Fire(new CannonModulesChangedSignal(_stateBuf.ToArray(), occupied, underAttack, total - occupied, total));
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
            GhostMode mode = IsOccupied.Value ? GhostMode.Solid : (_order == nextOrder ? GhostMode.Next : GhostMode.Pending);
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                var r = _ghostRenderers[i];
                if (r == null) continue;
                r.enabled = true;

                if (mode == GhostMode.Solid)
                {
                    if (_ghostMaterial != null && _originalMats[i] != null) r.sharedMaterials = _originalMats[i];
                    r.SetPropertyBlock(null);
                    continue;
                }

                float alpha = mode == GhostMode.Next ? _nextAlpha : _pendingAlpha;
                if (_ghostMaterial != null && _ghostMats[i] != null) r.sharedMaterials = _ghostMats[i];
                Color c = _ghostColor; c.a = alpha;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(_colorId, c);
                _mpb.SetFloat(_alphaId, alpha);
                r.SetPropertyBlock(_mpb);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Vector3 c = GhostCenter;
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.18f);
            Gizmos.DrawWireSphere(c, _attachDistance);
            UnityEditor.Handles.color = new Color(0.4f, 1f, 0.6f, 1f);
            UnityEditor.Handles.Label(c, $"Module {_order}");
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 c = GhostCenter;
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.95f);
            Gizmos.DrawWireSphere(c, _attachDistance);
            Gizmos.color = new Color(1f, 1f, 0.4f, 0.6f);
            Gizmos.DrawLine(transform.position, c);
        }
#endif
    }
}
