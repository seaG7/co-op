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
        private bool _wasHoldingCorpse;

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

            Vector3 eye = _look != null ? _look.EyePosition : transform.position;
            Vector3 aim = _look != null ? _look.AimDirection : transform.forward;
            if (!_carryService.TryGrab(carryable, base.Owner, eye, aim)) return;

            _heldItem = carryable;
            _heldByConnection = base.Owner;
            RpcGrabFx(carryable.transform.position);
            if (base.Owner != null && base.Owner.IsValid && !base.Owner.IsHost)
                SetHeldItemOnOwner(base.Owner, itemNob);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcGrabFx(Vector3 pos) => _signalBus?.Fire(new ItemPickedUpSignal(pos));

        [ObserversRpc(RunLocally = true)]
        private void RpcThrowFx(Vector3 pos) => _signalBus?.Fire(new ItemThrownSignal(pos));

        [ServerRpc]
        private void RequestRelease(Vector3 aimOrigin, Vector3 aimDir, Vector3 releasePos, Vector3 releaseVel)
        {
            if (_heldItem == null) return;
            var item = _heldItem;
            var dir = aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : transform.forward;

            bool soleHolder = _carryService.HolderCount(item) <= 1;
            WeaponModuleSlot slot = soleHolder
                ? FindAttachSlot(ModuleOrder(item), releasePos, aimOrigin, dir, SnapAimMinDot(_configs?.Carry))
                : null;

            if (slot != null)
            {
                _carryService.Release(item, base.Owner, Vector3.zero);
            }
            else
            {
                item.transform.position = releasePos;
                _carryService.Release(item, base.Owner, dir, releaseVel);
                RpcThrowFx(releasePos);
            }

            _heldItem = null;
            _heldByConnection = null;
            if (base.Owner != null && base.Owner.IsValid && !base.Owner.IsHost)
                SetHeldItemOnOwner(base.Owner, null);

            if (slot != null) AnimateAssembleAsync(item, slot).Forget();
        }

        private async UniTaskVoid AnimateAssembleAsync(Carryable item, WeaponModuleSlot slot)
        {
            if (item == null || slot == null) return;

            item.IsSnapped.Value = true;
            item.ApplyPhysicsState();

            Vector3 startPos = item.transform.position;
            Quaternion startRot = item.transform.rotation;

            var cc = _configs?.Carry;
            float reach = cc != null ? Mathf.Max(0.1f, cc.PickupReachSpeed) : 4f;
            float maxDur = cc != null ? cc.PickupMaxDuration : 0.7f;
            float dur = Mathf.Clamp(Vector3.Distance(startPos, slot.GhostCenter) / reach, 0.12f, maxDur);

            float t = 0f;
            while (t < dur)
            {
                if (item == null || slot == null) return;
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                item.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, slot.GhostCenter, k),
                    Quaternion.Slerp(startRot, slot.transform.rotation, k));
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (item == null || slot == null) return;

            slot.IsOccupied.Value = true;
            if (item.NetworkObject != null && base.IsServerInitialized)
                base.NetworkManager.ServerManager.Despawn(item.NetworkObject);
        }

        private static float SnapAimMinDot(CarryConfig carry)
            => carry != null ? Mathf.Cos(Mathf.Clamp(carry.SnapAimMaxAngle, 1f, 180f) * Mathf.Deg2Rad) : -1f;

        private static int ModuleOrder(Carryable item)
            => item != null && item.TryGetComponent<WeaponModulePart>(out var part) ? part.Order : 0;

        private static WeaponModuleSlot FindAttachSlot(int heldOrder, Vector3 itemPos,
            Vector3 aimOrigin, Vector3 aimDir, float minDot)
        {
            if (heldOrder <= 0 || heldOrder != WeaponModuleSlot.NextOrder()) return null;
            var slot = WeaponModuleSlot.Find(heldOrder);
            if (slot == null || !slot.IsFree) return null;

            Vector3 center = slot.GhostCenter;
            float maxDist = slot.AttachDistance;
            if ((itemPos - center).sqrMagnitude > maxDist * maxDist) return null;

            Vector3 toSlot = center - aimOrigin;
            float sqr = toSlot.sqrMagnitude;
            if (sqr >= 1e-6f && Vector3.Dot(aimDir, toSlot * (1f / Mathf.Sqrt(sqr))) < minDot) return null;
            return slot;
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

            if (held == null || _carryAnchor == null || held.IsSnapped.Value || held.Consuming.Value) return;

            if (held.HoldTuning)
            {
                held.CaptureHoldOffset(_carryAnchor);
                return;
            }

            held.GetHoldPose(_carryAnchor, out Vector3 targetPos, out Quaternion targetRot);

            if (_pickupElapsed < _pickupDuration)
            {
                if (advance) _pickupElapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_pickupElapsed / _pickupDuration));
                held.transform.SetPositionAndRotation(
                    Vector3.Lerp(_pickupFromPos, targetPos, k),
                    Quaternion.Slerp(_pickupFromRot, targetRot, k));
            }
            else
            {
                held.transform.SetPositionAndRotation(targetPos, targetRot);
            }
        }

        private void LateUpdate()
        {
            ApplyHeldPose(advance: true);

            if (!base.IsOwner) return;

            if (_interactionSuppressed || (_vitals != null && !_vitals.IsAlive))
            {
                SetPrompt(false);
                return;
            }

            if (_heldItem == null)
            {
                RaycastInteractable(out var hoverItem, out var hoverBottle);
                if (hoverItem != null && hoverItem.HolderClientId.Value == -1)
                    SetPrompt(true, InteractPromptKind.PickUp);
                else if (hoverBottle != null && !hoverBottle.IsClaimed)
                    SetPrompt(true, InteractPromptKind.Drink);
                else
                    SetPrompt(false);
                return;
            }

            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null || _configs?.Carry == null) return;
            var carry = _configs.Carry;

            float minDot = SnapAimMinDot(carry);
            bool canPlace = FindAttachSlot(ModuleOrder(_heldItem), _heldItem.transform.position,
                cam.transform.position, cam.transform.forward, minDot) != null;
            SetPrompt(true, canPlace ? InteractPromptKind.PlaceOnSocket : InteractPromptKind.Drop);
        }

        private void RaycastInteractable(out Carryable carryable, out Drinkable drinkable)
        {
            carryable = null;
            drinkable = null;
            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null || _configs?.Carry == null) return;
            const float probe = 50f;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit,
                    probe, _carryableMask, QueryTriggerInteraction.Ignore))
                return;
            if (Vector3.Distance(transform.position, hit.point) > _configs.Carry.MaxReach) return;
            carryable = hit.collider.GetComponentInParent<Carryable>();
            drinkable = hit.collider.GetComponentInParent<Drinkable>();
        }

        private void SetPrompt(bool show, InteractPromptKind kind = InteractPromptKind.PickUp)
        {
            if (show == _promptActive && (!show || kind == _promptKind)) return;
            _promptActive = show;
            _promptKind = kind;
            _signalBus?.Fire(new InteractPromptSignal(show, kind));
        }

        public float HeldMass => _heldItem != null ? _heldItem.Mass : 0f;

        public bool IsHolding => _heldItem != null;

        public Transform CarryAnchor => _carryAnchor;

        public Carryable CurrentHeld { get; private set; }

        private void Update()
        {
            if (base.IsServerInitialized && _heldItem != null && _heldItem.Consuming.Value)
            {
                _carryService.Release(_heldItem, _heldByConnection, Vector3.zero);
                _heldItem = null;
                _heldByConnection = null;
                if (base.Owner != null && base.Owner.IsValid && !base.Owner.IsHost)
                    SetHeldItemOnOwner(base.Owner, null);
            }

            if (_heldItem != null)
            {
                CurrentHeld = _heldItem;
            }
            else
            {
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

            if (!base.IsOwner) return;
            bool corpse = CurrentHeld != null && CurrentHeld.GetComponent<Corpse>() != null;
            if (corpse != _wasHoldingCorpse)
            {
                _wasHoldingCorpse = corpse;
                _signalBus?.Fire(new CorpseHeldSignal(corpse));
            }
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
