using Cysharp.Threading.Tasks;
using Data.Configs;
using FishNet.Connection;
using FishNet.Object;
using Gameplay.Player.Camera;
using Gameplay.Player.Look;
using Gameplay.Player.Movement;
using Gameplay.Player.Vitals;
using Gameplay.World.Items;
using Gameplay.World.Weapon;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Carry;
using Infrastructure.Services.Input;
using Signals;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Carry
{
    public sealed class PlayerCarry : NetworkBehaviour
    {
        [SerializeField] private LayerMask _carryableMask;
        [SerializeField] private PlayerCameraRig _cameraRig;
        [Tooltip("In-hand hold point in front of the chest (child of the player). Held item is pinned here rigidly.")]
        [SerializeField] private Transform _carryAnchor;

        [Inject] private IInputService _input;
        [Inject] private IConfigDataProvider _configs;
        [Inject] private SignalBus _signalBus;
        [Inject] private IPhysicalCarryService _carryService;

        private Carryable _heldItem;
        private PlayerLookController _look;
        private PlayerVitals _vitals;
        private bool _inputBound;

        private WeaponSnapPoint _highlightedSnap;

        private PlayerMovement _movement;

        private Carryable _attachItem;
        private Vector3 _pickupFromPos;
        private Quaternion _pickupFromRot;
        private float _pickupElapsed;
        private float _pickupDuration = 0.25f;
        public float PickupDuration => _pickupDuration;

        private bool _promptActive;
        private InteractPromptKind _promptKind;

        private NetworkConnection _heldByConnection;

        private bool _interactionSuppressed;

        private void Awake()
        {
            _look = GetComponent<PlayerLookController>();
            _vitals = GetComponent<PlayerVitals>();
            _movement = GetComponent<PlayerMovement>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner) BindInput();
        }

        public override void OnStopClient()
        {
            if (base.IsOwner)
            {
                UnbindInput();
                SetPrompt(false);
            }
            base.OnStopClient();
        }

        public override void OnStopServer()
        {
            if (_heldItem != null)
                _carryService.Release(_heldItem, _heldByConnection, Vector3.zero);
            _heldItem = null;
            _heldByConnection = null;
            base.OnStopServer();
        }

        private void OnDestroy()
        {
            UnbindInput();
            ClearSnapHighlight();
        }

        private void BindInput()
        {
            if (_inputBound || _input == null) return;
            _input.InteractStarted  += OnInteractStarted;
            _input.InteractCanceled += OnInteractCanceled;
            _inputBound = true;
        }

        private void UnbindInput()
        {
            if (!_inputBound || _input == null) return;
            _input.InteractStarted  -= OnInteractStarted;
            _input.InteractCanceled -= OnInteractCanceled;
            _inputBound = false;
        }

        private void OnInteractStarted()
        {
            if (_interactionSuppressed) return;
            if (_vitals != null && !_vitals.IsAlive) return;
            if (_heldItem != null) return;
            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null || _configs?.Carry == null) return;
            var carry = _configs.Carry;
            const float crosshairProbeDistance = 50f;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit,
                    crosshairProbeDistance, _carryableMask, QueryTriggerInteraction.Ignore))
                return;
            if (Vector3.Distance(transform.position, hit.point) > carry.MaxReach) return;
            var carryable = hit.collider.GetComponentInParent<Carryable>();
            if (carryable == null) return;
            RequestGrab(carryable.NetworkObject);
        }

        private void OnInteractCanceled()
        {
            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            Vector3 aimOrigin = cam != null ? cam.transform.position : transform.position;
            Vector3 aimDir = cam != null ? cam.transform.forward : transform.forward;
            Vector3 releasePos = _heldItem != null ? _heldItem.transform.position : transform.position;
            Vector3 releaseVel = _movement != null ? _movement.WorldVelocity : Vector3.zero;
            RequestRelease(aimOrigin, aimDir, releasePos, releaseVel);
        }

        [ServerRpc]
        private void RequestGrab(NetworkObject itemNob)
        {
            if (itemNob == null || _heldItem != null) return;
            var carryable = itemNob.GetComponent<Carryable>();
            if (carryable == null || carryable.Body == null) return;
            if (carryable.HolderClientId.Value != -1) return;

            var carry = _configs.Carry;
            if (Vector3.Distance(transform.position, carryable.transform.position) >
                carry.MaxReach * carry.ServerReachTolerance + carry.DefaultHoldDistance) return;

            if (carryable.IsSnapped.Value)
            {
                var snaps = WeaponSnapPoint.All;
                for (int i = 0; i < snaps.Count; i++)
                {
                    var s = snaps[i];
                    if (s != null && s.AttachedCarryable == carryable)
                    {
                        s.AttachedCarryable = null;
                        s.IsOccupied.Value = false;
                        break;
                    }
                }
            }

            Vector3 eye = _look != null ? _look.EyePosition : transform.position;
            Vector3 aim = _look != null ? _look.AimDirection : transform.forward;
            if (!_carryService.TryGrab(carryable, base.Owner, eye, aim)) return;

            _heldItem = carryable;
            _heldByConnection = base.Owner;
            if (base.Owner != null && base.Owner.IsValid && !base.Owner.IsHost)
                SetHeldItemOnOwner(base.Owner, itemNob);
        }

        [ServerRpc]
        private void RequestRelease(Vector3 aimOrigin, Vector3 aimDir, Vector3 releasePos, Vector3 releaseVel)
        {
            if (_heldItem == null) return;
            var item = _heldItem;
            var dir = aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : transform.forward;

            bool soleHolder = _carryService.HolderCount(item) <= 1;
            WeaponSnapPoint snap = null;
            Vector3? throwVel = null;

            if (soleHolder)
            {
                item.transform.position = releasePos;
                float tol = _configs?.Carry != null ? Mathf.Max(1f, _configs.Carry.ServerReachTolerance) : 1f;
                float minDot = SnapAimMinDot(_configs?.Carry);
                snap = FindNearestFreeSnapForServer(releasePos, tol, aimOrigin, dir, minDot);
                if (snap == null) throwVel = releaseVel;
            }

            _carryService.Release(item, base.Owner, snap != null ? Vector3.zero : dir, throwVel);

            _heldItem = null;
            _heldByConnection = null;
            if (base.Owner != null && base.Owner.IsValid && !base.Owner.IsHost)
                SetHeldItemOnOwner(base.Owner, null);

            if (snap != null) AnimateSnapAsync(item, snap).Forget();
        }

        private static float SnapAimMinDot(CarryConfig carry)
            => carry != null ? Mathf.Cos(Mathf.Clamp(carry.SnapAimMaxAngle, 1f, 180f) * Mathf.Deg2Rad) : -1f;

        private static bool IsSnapCandidate(WeaponSnapPoint s, Vector3 itemPos, float distanceMult,
            Vector3 aimOrigin, Vector3 aimDir, float minDot)
        {
            if (s == null || !s.IsFree) return false;

            float maxDist = s.SnapDistance * distanceMult;
            if ((itemPos - s.transform.position).sqrMagnitude > maxDist * maxDist) return false;

            Vector3 toSocket = s.transform.position - aimOrigin;
            float sqr = toSocket.sqrMagnitude;
            if (sqr < 1e-6f) return true;
            float dot = Vector3.Dot(aimDir, toSocket * (1f / Mathf.Sqrt(sqr)));
            return dot >= minDot;
        }

        private static WeaponSnapPoint FindNearestFreeSnapForServer(Vector3 itemPos, float distanceMult,
            Vector3 aimOrigin, Vector3 aimDir, float minDot)
        {
            WeaponSnapPoint best = null;
            float bestDistSq = float.MaxValue;
            var all = WeaponSnapPoint.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (!IsSnapCandidate(s, itemPos, distanceMult, aimOrigin, aimDir, minDot)) continue;
                float dSq = (itemPos - s.transform.position).sqrMagnitude;
                if (dSq < bestDistSq) { bestDistSq = dSq; best = s; }
            }
            return best;
        }

        private const float SnapAnimationDurationSec = 0.25f;

        private async UniTaskVoid AnimateSnapAsync(Carryable item, WeaponSnapPoint snap)
        {
            snap.AttachedCarryable = item;
            snap.IsOccupied.Value = true;
            item.IsSnapped.Value = true;
            item.ApplyPhysicsState();

            Vector3 startPos = item.transform.position;
            Quaternion startRot = item.transform.rotation;
            Vector3 endPos = snap.transform.position;
            Quaternion endRot = snap.transform.rotation;

            float t = 0f;
            while (t < SnapAnimationDurationSec)
            {
                if (item == null || !item.IsSnapped.Value) return;
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / SnapAnimationDurationSec));
                item.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, endPos, k),
                    Quaternion.Slerp(startRot, endRot, k));
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            if (item == null || !item.IsSnapped.Value) return;

            item.transform.SetPositionAndRotation(endPos, endRot);
            item.HasBeenGrabbedOnce.Value = false;
        }

        [TargetRpc]
        private void SetHeldItemOnOwner(NetworkConnection conn, NetworkObject itemNob)
        {
            _heldItem = itemNob != null ? itemNob.GetComponent<Carryable>() : null;
        }

        public void PinForIK() => ApplyHeldPose(advance: false);

        private void ApplyHeldPose(bool advance)
        {
            var held = CurrentHeld;
            if (held != _attachItem)
            {
                _attachItem = held;
                if (held != null && _carryAnchor != null)
                {
                    _pickupFromPos = held.transform.position;
                    _pickupFromRot = held.transform.rotation;
                    _pickupElapsed = 0f;
                    var cc = _configs?.Carry;
                    float reach = cc != null ? Mathf.Max(0.1f, cc.PickupReachSpeed) : 4f;
                    float maxDur = cc != null ? cc.PickupMaxDuration : 0.7f;
                    float dist = Vector3.Distance(_pickupFromPos, _carryAnchor.position);
                    _pickupDuration = Mathf.Clamp(dist / reach, 0.12f, maxDur);
                }
            }

            if (held == null || _carryAnchor == null || held.IsSnapped.Value) return;

            if (_pickupElapsed < _pickupDuration)
            {
                if (advance) _pickupElapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_pickupElapsed / _pickupDuration));
                held.transform.SetPositionAndRotation(
                    Vector3.Lerp(_pickupFromPos, _carryAnchor.position, k),
                    Quaternion.Slerp(_pickupFromRot, _carryAnchor.rotation, k));
            }
            else
            {
                held.transform.SetPositionAndRotation(_carryAnchor.position, _carryAnchor.rotation);
            }
        }

        private void LateUpdate()
        {
            ApplyHeldPose(advance: true);

            if (!base.IsOwner) return;

            if (_interactionSuppressed || (_vitals != null && !_vitals.IsAlive))
            {
                ClearSnapHighlight();
                SetPrompt(false);
                return;
            }

            if (_heldItem == null)
            {
                ClearSnapHighlight();
                var hover = RaycastForCarryable();
                bool canPick = hover != null && hover.HolderClientId.Value == -1;
                SetPrompt(canPick, InteractPromptKind.PickUp);
                return;
            }

            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null || _configs?.Carry == null) return;
            var carry = _configs.Carry;

            UpdateSnapHighlight(_heldItem.transform.position);

            float minDot = SnapAimMinDot(carry);
            float tol = Mathf.Max(1f, carry.ServerReachTolerance);
            bool canPlace = HasSnapCandidate(_heldItem.transform.position, tol,
                cam.transform.position, cam.transform.forward, minDot);
            SetPrompt(true, canPlace ? InteractPromptKind.PlaceOnSocket : InteractPromptKind.Drop);
        }

        private static bool HasSnapCandidate(Vector3 itemPos, float distanceMult,
            Vector3 aimOrigin, Vector3 aimDir, float minDot)
        {
            var all = WeaponSnapPoint.All;
            for (int i = 0; i < all.Count; i++)
                if (IsSnapCandidate(all[i], itemPos, distanceMult, aimOrigin, aimDir, minDot)) return true;
            return false;
        }

        private Carryable RaycastForCarryable()
        {
            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null || _configs?.Carry == null) return null;
            const float probe = 50f;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit,
                    probe, _carryableMask, QueryTriggerInteraction.Ignore))
                return null;
            if (Vector3.Distance(transform.position, hit.point) > _configs.Carry.MaxReach) return null;
            return hit.collider.GetComponentInParent<Carryable>();
        }

        private void SetPrompt(bool show, InteractPromptKind kind = InteractPromptKind.PickUp)
        {
            if (show == _promptActive && (!show || kind == _promptKind)) return;
            _promptActive = show;
            _promptKind = kind;
            _signalBus?.Fire(new InteractPromptSignal(show, kind));
        }

        private void UpdateSnapHighlight(Vector3 itemPos)
        {
            WeaponSnapPoint nearest = null;
            float bestDistSq = float.MaxValue;
            var all = WeaponSnapPoint.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || !s.IsFree) continue;
                float dSq = (itemPos - s.transform.position).sqrMagnitude;
                float maxSq = s.HighlightDistance * s.HighlightDistance;
                if (dSq <= maxSq && dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    nearest = s;
                }
            }

            if (nearest == _highlightedSnap) return;
            if (_highlightedSnap != null) _highlightedSnap.SetHighlight(false);
            _highlightedSnap = nearest;
            if (_highlightedSnap != null) _highlightedSnap.SetHighlight(true);
        }

        private void ClearSnapHighlight()
        {
            if (_highlightedSnap == null) return;
            _highlightedSnap.SetHighlight(false);
            _highlightedSnap = null;
        }

        public float HeldMass => _heldItem != null ? _heldItem.Mass : 0f;

        public bool IsHolding => _heldItem != null;

        public Carryable CurrentHeld { get; private set; }

        private void Update()
        {
            if (_heldItem != null) { CurrentHeld = _heldItem; return; }

            var all = Carryable.All;
            int myId = base.OwnerId;
            Carryable found = null;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.HolderClientId.Value == myId) { found = c; break; }
            }
            CurrentHeld = found;
        }

        public void SetInteractionSuppressed(bool suppressed) => _interactionSuppressed = suppressed;

        public void ServerForceDrop()
        {
            if (!base.IsServerInitialized || _heldItem == null) return;
            var item = _heldItem;
            _carryService.Release(item, _heldByConnection, Vector3.zero);
            _heldItem = null;
            _heldByConnection = null;
            if (base.Owner != null && base.Owner.IsValid && !base.Owner.IsHost)
                SetHeldItemOnOwner(base.Owner, null);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var carry = _configs?.Carry;
            if (carry == null || _cameraRig == null || _cameraRig.Camera == null) return;
            if (carry.DebugDrawRaycast)
            {
                Gizmos.color = Color.cyan;
                var p = _cameraRig.Camera.transform.position;
                var d = _cameraRig.Camera.transform.forward;
                Gizmos.DrawLine(p, p + d * carry.MaxReach);
            }
            if (carry.DebugDrawGrab && _heldItem != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_heldItem.transform.position, 0.05f);
            }
        }

        private void OnGUI()
        {
            var carry = _configs?.Carry;
            if (carry == null || !carry.DebugOverlay) return;
            if (!base.IsOwner) return;
            var label = _heldItem != null
                ? $"Holding: {_heldItem.name} | Mass: {_heldItem.Mass:F1} (FreeCarry: {carry.FreeCarryMass:F1})"
                : "Holding: -";
            GUI.Label(new Rect(10, 10, 600, 24), label);
        }
#endif
    }
}
