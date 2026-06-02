using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Object;
using Gameplay.Player.Camera;
using Gameplay.World.Items;
using Gameplay.World.Weapon;
using Infrastructure.Providers.Configs;
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

        [Inject] private IInputService _input;
        [Inject] private IConfigDataProvider _configs;
        [Inject] private SignalBus _signalBus;

        private Carryable _heldItem;

        private UnityEngine.CharacterController _cc;
        private bool _inputBound;

        private WeaponSnapPoint _highlightedSnap;

        private Carryable _carryTrackedItem;
        private float _pickupBlend;
        private readonly System.Collections.Generic.List<Collider> _ignoredColliders = new();

        private bool _promptActive;

        private Vector3 _lastHeldPos;
        private Vector3 _heldVel;

        private NetworkConnection _heldByConnection;

        private void Awake()
        {
            _cc = GetComponent<UnityEngine.CharacterController>();
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
            ForceRelease(Vector3.zero);
            base.OnStopServer();
        }

        private void OnDestroy()
        {
            UnbindInput();
            ClearSnapHighlight();
            SetCarryCollisionIgnored(false);
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
            if (_heldItem != null) return;
            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null) return;
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

        private void OnInteractCanceled() => RequestRelease(_heldVel);

        [ServerRpc]
        private void RequestGrab(NetworkObject itemNob)
        {
            if (itemNob == null) return;
            if (_heldItem != null) return;
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
                carryable.IsSnapped.Value = false;
            }

            carryable.HolderClientId.Value = base.OwnerId;
            carryable.HasBeenGrabbedOnce.Value = true;
            carryable.Body.isKinematic = true;
            itemNob.GiveOwnership(base.Owner);

            _heldItem = carryable;
            _heldByConnection = base.Owner;
            if (base.Owner != null && base.Owner.IsValid && !base.Owner.IsHost)
                SetHeldItemOnOwner(base.Owner, itemNob);
        }

        [ServerRpc]
        private void RequestRelease(Vector3 throwVel)
        {
            if (_heldItem == null) return;
            var snap = FindNearestFreeSnapForServer(_heldItem.transform.position);
            if (snap != null)
            {
                AnimateSnapAsync(_heldItem, snap).Forget();
                return;
            }
            ForceRelease(throwVel);
        }

        private static WeaponSnapPoint FindNearestFreeSnapForServer(Vector3 origin)
        {
            WeaponSnapPoint best = null;
            float bestDistSq = float.MaxValue;
            var all = WeaponSnapPoint.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || !s.IsFree) continue;
                float dSq = (origin - s.transform.position).sqrMagnitude;
                float maxSq = s.SnapDistance * s.SnapDistance;
                if (dSq <= maxSq && dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = s;
                }
            }
            return best;
        }

        private const float SnapAnimationDurationSec = 0.25f;

        private async UniTaskVoid AnimateSnapAsync(Carryable item, WeaponSnapPoint snap)
        {
            snap.AttachedCarryable = item;
            snap.IsOccupied.Value = true;
            item.IsSnapped.Value = true;

            var nob = item.NetworkObject;
            if (nob != null) nob.RemoveOwnership();

            if (item.Body != null) item.Body.isKinematic = true;
            item.HolderClientId.Value = -1;

            var holder = _heldByConnection;
            _heldItem = null;
            _heldByConnection = null;
            if (holder != null && holder.IsValid && !holder.IsHost)
                SetHeldItemOnOwner(holder, null);

            Vector3 startPos = item.transform.position;
            Quaternion startRot = item.transform.rotation;
            Vector3 endPos = snap.transform.position;
            Quaternion endRot = snap.transform.rotation;

            float t = 0f;
            while (t < SnapAnimationDurationSec)
            {
                if (item == null) return;
                if (!item.IsSnapped.Value) return;
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

        private void ForceRelease(Vector3 throwVel)
        {
            if (_heldItem == null) return;
            var rb = _heldItem.Body;
            var nob = _heldItem.NetworkObject;
            if (rb != null)
            {
                rb.isKinematic = !_heldItem.HasBeenGrabbedOnce.Value;
                if (!rb.isKinematic && _configs?.Carry != null)
                {
                    var carry = _configs.Carry;
                    Vector3 v = Vector3.ClampMagnitude(throwVel * carry.ThrowVelocityScale, carry.MaxThrowSpeed);
                    rb.linearVelocity = v;
                }
            }
            _heldItem.HolderClientId.Value = -1;
            if (nob != null) nob.RemoveOwnership();

            var holder = _heldByConnection;
            _heldItem = null;
            _heldByConnection = null;
            if (holder != null && holder.IsValid && !holder.IsHost)
                SetHeldItemOnOwner(holder, null);
        }

        [TargetRpc]
        private void SetHeldItemOnOwner(NetworkConnection conn, NetworkObject itemNob)
        {
            _heldItem = itemNob != null ? itemNob.GetComponent<Carryable>() : null;
        }

        private void LateUpdate()
        {
            if (!base.IsOwner) return;

            SyncCarryTracking();

            if (_heldItem == null)
            {
                ClearSnapHighlight();
                var hover = RaycastForCarryable();
                SetPrompt(hover != null && hover.HolderClientId.Value == -1);
                return;
            }

            SetPrompt(false);

            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null) return;
            var carry = _configs.Carry;

            float dist = _heldItem.HoldDistance > 0f ? _heldItem.HoldDistance : carry.DefaultHoldDistance;
            Vector3 handTarget = cam.transform.position + cam.transform.forward * dist + cam.transform.up * -0.1f;
            Quaternion handRot = cam.transform.rotation;

            float blendDur = Mathf.Max(0.0001f, carry.PickupBlendDuration);
            _pickupBlend = Mathf.MoveTowards(_pickupBlend, 1f, Time.deltaTime / blendDur);
            float k = Mathf.SmoothStep(0f, 1f, _pickupBlend);
            Vector3 pos = Vector3.Lerp(_heldItem.transform.position, handTarget, k);
            Quaternion rot = Quaternion.Slerp(_heldItem.transform.rotation, handRot, k);
            _heldItem.transform.SetPositionAndRotation(pos, rot);

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            if (_pickupBlend >= 0.999f)
            {
                Vector3 inst = (pos - _lastHeldPos) / dt;
                _heldVel = Vector3.Lerp(_heldVel, inst, 0.5f);
            }
            _lastHeldPos = pos;

            UpdateSnapHighlight(_heldItem.transform.position);
        }

        private void SyncCarryTracking()
        {
            if (_heldItem == _carryTrackedItem) return;

            SetCarryCollisionIgnored(false);
            _carryTrackedItem = _heldItem;
            _pickupBlend = 0f;
            _heldVel = Vector3.zero;
            if (_carryTrackedItem != null)
            {
                _lastHeldPos = _carryTrackedItem.transform.position;
                SetCarryCollisionIgnored(true);
            }
        }

        private void SetCarryCollisionIgnored(bool ignored)
        {
            if (_cc == null) return;

            if (ignored)
            {
                _ignoredColliders.Clear();
                if (_carryTrackedItem == null) return;
                _carryTrackedItem.GetComponentsInChildren(true, _ignoredColliders);
                for (int i = 0; i < _ignoredColliders.Count; i++)
                {
                    var col = _ignoredColliders[i];
                    if (col != null && !col.isTrigger) Physics.IgnoreCollision(_cc, col, true);
                }
            }
            else
            {
                for (int i = 0; i < _ignoredColliders.Count; i++)
                {
                    var col = _ignoredColliders[i];
                    if (col != null) Physics.IgnoreCollision(_cc, col, false);
                }
                _ignoredColliders.Clear();
            }
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

        private void SetPrompt(bool show)
        {
            if (show == _promptActive) return;
            _promptActive = show;
            _signalBus?.Fire(new InteractPromptSignal(show));
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
                : "Holding: —";
            GUI.Label(new Rect(10, 10, 600, 24), label);
        }
#endif
    }
}
