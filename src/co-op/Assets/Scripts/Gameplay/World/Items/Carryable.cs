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

        [Inject] private SignalBus _signalBus;
        [Inject] private Infrastructure.Providers.Configs.IConfigDataProvider _configs;

        public readonly SyncVar<int> HolderClientId = new(-1);
        public readonly SyncVar<bool> HasBeenGrabbedOnce = new(false);
        public readonly SyncVar<bool> IsSnapped = new(false);

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

        public float FragileImpulse => (_config != null && _config.FragileImpulse > 0f) ? _config.FragileImpulse : -1f;

        private Collider[] _colliders;

        private void Awake()
        {
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
            ApplyPhysicsState();
            PlayerItemPhysics.RegisterItem(this);
        }

        public override void OnStopNetwork()
        {
            PlayerItemPhysics.UnregisterItem(this);
            HolderClientId.OnChange -= OnHolderChanged;
            HasBeenGrabbedOnce.OnChange -= OnFlagChanged;
            IsSnapped.OnChange -= OnFlagChanged;
            _all.Remove(this);
            base.OnStopNetwork();
        }

        private void OnHolderChanged(int prev, int next, bool asServer)
        {
            if (_networkTransform != null) _networkTransform.enabled = next == -1;
            ApplyPhysicsState();
        }
        private void OnFlagChanged(bool prev, bool next, bool asServer) => ApplyPhysicsState();

        public void ApplyPhysicsState()
        {
            if (_rb == null) return;

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
