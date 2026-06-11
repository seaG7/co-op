using Data.Configs;
using FishNet.Object.Synchronizing;
using Gameplay.Net;
using Gameplay.Player.Animation;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Items
{

    [RequireComponent(typeof(Rigidbody))]
    public sealed class Carryable : InjectableNetworkBehaviour
    {
        [Header("Config (designer-assigned at edit time on the prefab)")]
        [SerializeField] private InteractableItemConfig _config;

        [Header("Wired components")]
        [SerializeField] private Rigidbody _rb;
        [Tooltip("Disabled while held (the holder positions the item rigidly in-hand client-side); re-enabled on release so the server replicates the free/snapped item.")]
        [SerializeField] private FishNet.Component.Transforming.NetworkTransform _networkTransform;

        [Header("Grab anchors (child transforms authored on the prefab). Index 0 for one-hand; 0 and 1 for two-hand.")]
        [SerializeField] private Transform[] _grabAnchors;

        [Header("Per-hand IK grip points (child transforms at the real grip edges; for the visual hand IK).")]
        [SerializeField] private Transform _leftHandGrip;
        [SerializeField] private Transform _rightHandGrip;

        [Header("Per-hand IK elbow hints (optional child transforms; the elbow is pulled toward these).")]
        [SerializeField] private Transform _leftElbowHint;
        [SerializeField] private Transform _rightElbowHint;

        [Header("In-hand pose (offset from the player's carry anchor — tune in Play Mode + bake)")]
        [Tooltip("Local position of the item relative to the carry anchor while held.")]
        [SerializeField] private Vector3 _holdPositionOffset;
        [Tooltip("Local rotation (euler) of the item relative to the carry anchor while held.")]
        [SerializeField] private Vector3 _holdEulerOffset;

        [Inject] private SignalBus _signalBus;
        [Inject] private Infrastructure.Providers.Configs.IConfigDataProvider _configs;

        public readonly SyncVar<int> HolderClientId = new(-1);
        public readonly SyncVar<bool> HasBeenGrabbedOnce = new(false);
        public readonly SyncVar<bool> IsSnapped = new(false);

        public readonly SyncVar<bool> Consuming = new(false);
        private readonly SyncVar<Vector3> _consumeStart = new();
        private readonly SyncVar<Vector3> _consumeEnd = new();
        private readonly SyncVar<float> _consumeProgress = new(0f);
        private float _consumeDuration = 0.6f;
        private Vector3 _baseScale = Vector3.one;

        public Rigidbody Body => _rb;
        public InteractableItemConfig Config => _config;

        public float Mass => _config != null ? _config.Mass : (_rb != null ? _rb.mass : 1f);
        public float MaxCarrySpeed => _config != null ? _config.MaxCarrySpeed : 5f;
        public float HoldDistance => _config != null ? _config.HoldDistance : 0f;

        public bool IsHeld => HolderClientId.Value != -1;

        public int AnchorCount => _grabAnchors != null ? _grabAnchors.Length : 0;
        public int HoldersRequired => _config != null ? Mathf.Max(1, _config.MinHolders) : 1;
        public Transform GetAnchor(int i)
            => (_grabAnchors != null && i >= 0 && i < _grabAnchors.Length && _grabAnchors[i] != null) ? _grabAnchors[i] : transform;
        public Vector3 AnchorLocalPosition(int i)
        {
            var a = GetAnchor(i);
            return a == transform ? Vector3.zero : transform.InverseTransformPoint(a.position);
        }

        private static readonly System.Collections.Generic.List<Carryable> _all = new();
        public static System.Collections.Generic.IReadOnlyList<Carryable> All => _all;

        public bool UsesTwoHands => _config != null && _config.UsesTwoHands;

        public Transform HandGrip(HandSide side)
        {
            if (side == HandSide.Left) return _leftHandGrip != null ? _leftHandGrip : transform;
            return _rightHandGrip != null ? _rightHandGrip : transform;
        }

        public Transform RawGrip(HandSide side) => side == HandSide.Left ? _leftHandGrip : _rightHandGrip;
        public Transform ElbowHint(HandSide side) => side == HandSide.Left ? _leftElbowHint : _rightElbowHint;

#if UNITY_EDITOR
        public void EditorSetGrip(HandSide side, Transform t) { if (side == HandSide.Left) _leftHandGrip = t; else _rightHandGrip = t; }
        public void EditorSetElbowHint(HandSide side, Transform t) { if (side == HandSide.Left) _leftElbowHint = t; else _rightElbowHint = t; }
        public void EditorSetHoldPose(Vector3 posOffset, Vector3 eulerOffset) { _holdPositionOffset = posOffset; _holdEulerOffset = eulerOffset; }
#endif

        public Vector3 HoldPositionOffset => _holdPositionOffset;
        public Vector3 HoldEulerOffset => _holdEulerOffset;

        [System.NonSerialized] public bool HoldTuning;

        public void GetHoldPose(Transform carryAnchor, out Vector3 pos, out Quaternion rot)
        {
            if (carryAnchor == null) { pos = transform.position; rot = transform.rotation; return; }
            pos = carryAnchor.TransformPoint(_holdPositionOffset);
            rot = carryAnchor.rotation * Quaternion.Euler(_holdEulerOffset);
        }

        public void CaptureHoldOffset(Transform carryAnchor)
        {
            if (carryAnchor == null) return;
            _holdPositionOffset = carryAnchor.InverseTransformPoint(transform.position);
            _holdEulerOffset = (Quaternion.Inverse(carryAnchor.rotation) * transform.rotation).eulerAngles;
        }

        public float FragileImpulse => (_config != null && _config.FragileImpulse > 0f) ? _config.FragileImpulse : -1f;

        private Collider[] _colliders;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_networkTransform == null) _networkTransform = GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            _colliders = GetComponentsInChildren<Collider>(true);
            if (_config != null && _rb != null) _rb.mass = _config.Mass;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_all.Contains(this)) _all.Add(this);
            HolderClientId.OnChange += OnHolderChanged;
            HasBeenGrabbedOnce.OnChange += OnFlagChanged;
            IsSnapped.OnChange += OnFlagChanged;
            Consuming.OnChange += OnFlagChanged;
            ApplyPhysicsState();
            PlayerItemPhysics.RegisterItem(this);
        }

        public override void OnStopNetwork()
        {
            PlayerItemPhysics.UnregisterItem(this);
            HolderClientId.OnChange -= OnHolderChanged;
            HasBeenGrabbedOnce.OnChange -= OnFlagChanged;
            IsSnapped.OnChange -= OnFlagChanged;
            Consuming.OnChange -= OnFlagChanged;
            _all.Remove(this);
            base.OnStopNetwork();
        }

        private void OnHolderChanged(int prev, int next, bool asServer)
        {
            if (Consuming.Value) { ApplyPhysicsState(); return; }
            if (_networkTransform != null) _networkTransform.enabled = next == -1;
            ApplyPhysicsState();
        }
        private void OnFlagChanged(bool prev, bool next, bool asServer) => ApplyPhysicsState();

        public void ApplyPhysicsState()
        {
            if (_rb == null) return;

            if (Consuming.Value)
            {
                if (_networkTransform != null) _networkTransform.enabled = false;
                if (_colliders != null)
                    for (int i = 0; i < _colliders.Length; i++)
                        if (_colliders[i] != null) _colliders[i].enabled = false;
                _rb.detectCollisions = false;
                if (!_rb.isKinematic)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                    _rb.isKinematic = true;
                }
                return;
            }

            bool held = HolderClientId.Value != -1;
            bool snapped = IsSnapped.Value;

            _rb.detectCollisions = !held;
            if (_colliders != null)
                for (int i = 0; i < _colliders.Length; i++)
                    if (_colliders[i] != null) _colliders[i].enabled = !held;

            bool serverDynamic = IsServerInitialized && !snapped && !held && HasBeenGrabbedOnce.Value;
            bool kinematic = !serverDynamic;

            if (_rb.isKinematic == kinematic) return;

            if (kinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
            else
            {
                _rb.isKinematic = false;
            }
        }

        public void ServerMakeDynamic()
        {
            if (!IsServerInitialized) return;
            HasBeenGrabbedOnce.Value = true;
            ApplyPhysicsState();
        }

        public void ServerBeginConsume(Vector3 start, Vector3 end, float duration)
        {
            if (!IsServerInitialized || Consuming.Value) return;
            _consumeDuration = Mathf.Max(0.05f, duration);
            _consumeStart.Value = start;
            _consumeEnd.Value = end;
            _consumeProgress.Value = 0f;
            HolderClientId.Value = -1;
            Consuming.Value = true;
            ApplyPhysicsState();
        }

        private void Update()
        {
            if (!Consuming.Value) return;
            float p = _consumeProgress.Value;
            transform.position = Vector3.Lerp(_consumeStart.Value, _consumeEnd.Value, p);
            transform.localScale = Vector3.Lerp(_baseScale, Vector3.zero, p);
            if (IsServerInitialized && p < 1f)
                _consumeProgress.Value = Mathf.Clamp01(p + Time.deltaTime / _consumeDuration);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServerInitialized || _rb == null || _rb.isKinematic || _signalBus == null) return;
            float threshold = FragileImpulse > 0f
                ? FragileImpulse
                : (_configs?.Carry != null ? _configs.Carry.DefaultFragileImpulse : 6f);
            float impulse = collision.impulse.magnitude;
            if (impulse >= threshold)
                _signalBus.Fire(new ItemImpactSignal(collision.GetContact(0).point, impulse));
        }
    }
}
