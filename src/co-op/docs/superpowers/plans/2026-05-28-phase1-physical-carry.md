# Phase 1 Physical Carry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the physics-driven carry primitive — aim-based grab (camera raycast, `connectedAnchor = hit point`), Hold-to-grip, server-authoritative `ConfigurableJoint`, two-handed via physics emergence, throw via preserved inertia. Items spawn at procgen anchors with probability, one universal prefab driven by `ComponentItemPreset` SOs. All tuning lives in `CarryConfig` SO + presets — designers tweak assets, no code. Editor tools (Spawn Test Items menu, debug gizmo/overlay, live-apply context menu) ship in this milestone for fast iteration.

**Architecture:** Server-authoritative physics (`NetworkRigidbody`). Joint on server only. Carryable replicates preset choice via `SyncVar<int>` against an `IComponentPresetRegistry` loaded from Resources on both sides. Spawn fires on `WorldGeneratedSignal`. See spec `docs/superpowers/specs/2026-05-28-phase1-physical-carry-design.md` for full rationale.

**Tech Stack:** Unity 6 URP, FishNet 4.x (`NetworkBehaviour` / `SyncVar<T>` / `NetworkRigidbody`), Zenject (Project/Scene/GameObjectContext), UniTask, custom `Signals.SignalBus`, ScriptableObject configs via `IConfigDataProvider`. Asset edits done via UnityMCP (`set_active_instance("co-op@..."), refresh_unity(wait_for_ready=false), manage_scene/manage_gameobject/manage_components/manage_scriptable_object`); play-mode QA is the user's part.

---

### Task 1: Config plumbing — `ComponentSpawnChance` + `CARRY_CONFIG_PATH`

**Files:**
- Modify: `Assets/Scripts/Data/Configs/WorldGenConfig.cs`
- Modify: `Assets/Scripts/Data/Paths/ConfigPaths.cs`

- [ ] **Step 1: Add `ComponentSpawnChance` to WorldGenConfig**

Insert in `WorldGenConfig` (after MaxAnchorSlope, before closing brace):
```csharp
[Header("Components")]
[Tooltip("Probability per ComponentSpawn anchor that an item will spawn on it.")]
[Range(0f, 1f)]
public float ComponentSpawnChance = 0.75f;
```

- [ ] **Step 2: Add `CARRY_CONFIG_PATH` const**

In `ConfigPaths`:
```csharp
public const string CARRY_CONFIG_PATH = "Configs/Carry/CarryConfig";
```

- [ ] **Step 3: Recompile & verify**

`refresh_unity(wait_for_ready=false)` → poll `editor/state` → `read_console filter="error CS"` → expect 0.

---

### Task 2: `CarryConfig` + `ComponentItemPreset` SO classes

**Files:**
- Create: `Assets/Scripts/Data/Configs/CarryConfig.cs`
- Create: `Assets/Scripts/Data/Configs/ComponentItemPreset.cs`

- [ ] **Step 1: Write `CarryConfig.cs`**

```csharp
using UnityEngine;

namespace Data.Configs
{
    /// <summary>
    /// Single source of truth for carry tuning. Everything a designer / tester might tweak
    /// lives here — joint drives, reach, release boost, debug toggles. No magic numbers in code.
    /// Loaded from Resources/Configs/Carry/CarryConfig.asset via IConfigDataProvider.Carry.
    /// </summary>
    [CreateAssetMenu(menuName = "Configs/Carry Config", fileName = "CarryConfig")]
    public sealed class CarryConfig : ScriptableObject
    {
        [Header("Reach")]
        [Tooltip("Raycast distance from camera for picking up.")]
        public float MaxReach = 1.5f;
        [Tooltip("Multiplier on MaxReach for server-side validation (latency forgiveness).")]
        public float ServerReachTolerance = 1.2f;

        [Header("Joint linear drive")]
        public float JointLinearSpring = 8000f;
        public float JointLinearDamper = 200f;
        [Tooltip("Per-axis max force the linear drive can apply. Determines how heavy an item one joint can lift.")]
        public float JointMaxForce = 200f;
        [Tooltip("Soft positional limit (m). Smaller = rigid grip; larger = rubbery.")]
        public float JointLinearLimit = 0.05f;

        [Header("Joint angular drive (sway feel)")]
        public float JointAngularSpring = 500f;
        public float JointAngularDamper = 50f;

        [Header("Release")]
        [Tooltip(">1 = snappier throw; 1 = pure physics inertia.")]
        public float ReleaseVelocityBoost = 1.0f;

        [Header("Debug")]
        public bool DebugDrawRaycast = false;
        public bool DebugDrawGrab = false;
        public bool DebugOverlay = false;

        [ContextMenu("Reset to defaults")]
        private void ResetToDefaults()
        {
            MaxReach = 1.5f; ServerReachTolerance = 1.2f;
            JointLinearSpring = 8000f; JointLinearDamper = 200f; JointMaxForce = 200f; JointLinearLimit = 0.05f;
            JointAngularSpring = 500f; JointAngularDamper = 50f;
            ReleaseVelocityBoost = 1.0f;
            DebugDrawRaycast = DebugDrawGrab = DebugOverlay = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
```

- [ ] **Step 2: Write `ComponentItemPreset.cs`**

```csharp
using UnityEngine;

namespace Data.Configs
{
    /// <summary>
    /// A spawnable variant of ComponentItem.prefab. Carryable.ApplyPreset reads these
    /// to configure mesh, material, mass, scale on the runtime instance. Adding a new
    /// item type = create a new asset; no prefab or code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Configs/Component Item Preset", fileName = "ComponentItemPreset")]
    public sealed class ComponentItemPreset : ScriptableObject
    {
        public Mesh Mesh;
        public Material Material;
        [Tooltip("Rigidbody mass (kg). Gates one-handed vs two-handed via physics emergence.")]
        public float Mass = 2f;
        public Vector3 Scale = Vector3.one;
    }
}
```

- [ ] **Step 3: Recompile & verify** — 0 CS errors.

---

### Task 3: Provider integration — `IConfigDataProvider.Carry`

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Providers/Configs/IConfigDataProvider.cs`
- Modify: `Assets/Scripts/Infrastructure/Providers/Configs/ConfigDataProvider.cs`

- [ ] **Step 1: Add `CarryConfig Carry { get; }` to `IConfigDataProvider`**

Extend the interface alongside `Windows`, `Network`, `Movement`, `World`.

- [ ] **Step 2: Extend `ConfigDataProvider.LoadAsync`**

Add a field `private CarryConfig _carry;` and `public CarryConfig Carry => _carry;`. Update the `UniTask.WhenAll` tuple to include a 5th element loading `ConfigPaths.CARRY_CONFIG_PATH`. Mirror the existing pattern verbatim.

- [ ] **Step 3: Recompile & verify** — 0 CS errors.

---

### Task 4: Resources assets — `CarryConfig.asset` + 3 presets

**Files (created via MCP `manage_scriptable_object`):**
- Create: `Assets/Resources/Configs/Carry/CarryConfig.asset`
- Create: `Assets/Resources/Configs/Carry/Presets/Preset_LightCube.asset`
- Create: `Assets/Resources/Configs/Carry/Presets/Preset_MediumSphere.asset`
- Create: `Assets/Resources/Configs/Carry/Presets/Preset_HeavyLong.asset`

- [ ] **Step 1: Create `CarryConfig.asset`** with all defaults from the SO. MCP: `manage_scriptable_object` create at the path with default values.

- [ ] **Step 2: Create 3 preset assets** with these values:

| Asset                       | Mesh (built-in)                  | Mass | Scale          |
|-----------------------------|----------------------------------|------|----------------|
| `Preset_LightCube.asset`    | `Library/.../Cube.fbx → Cube`    | 2    | `(0.4,0.4,0.4)`|
| `Preset_MediumSphere.asset` | `.../Sphere.fbx → Sphere`        | 8    | `(0.5,0.5,0.5)`|
| `Preset_HeavyLong.asset`    | `.../Cube.fbx → Cube`            | 30   | `(1.5,0.3,0.3)`|

Materials: leave null in this step; create a placeholder URP/Lit material per preset later (Task 12 if needed, or assign user-side). The Carryable will gracefully handle null material (skip the assignment).

- [ ] **Step 3: Verify** assets exist via `manage_asset action=search filter_type=ScriptableObject path=Assets/Resources/Configs/Carry`.

---

### Task 5: Layer `Carryable` in TagManager

**Files:**
- Modify: `ProjectSettings/TagManager.asset` (via MCP `manage_editor action=add_layer`).

- [ ] **Step 1: Add layer**

```
manage_editor(action="add_layer", layer_name="Carryable")
```

- [ ] **Step 2: Verify** via the `mcpforunity://project/layers` resource: expect `Carryable` present.

---

### Task 6: `IComponentPresetRegistry` + `ComponentPresetRegistry`

**Files:**
- Create: `Assets/Scripts/Infrastructure/Services/Carry/IComponentPresetRegistry.cs`
- Create: `Assets/Scripts/Infrastructure/Services/Carry/ComponentPresetRegistry.cs`

- [ ] **Step 1: Interface**

```csharp
using Data.Configs;

namespace Infrastructure.Services.Carry
{
    public interface IComponentPresetRegistry
    {
        int Count { get; }
        ComponentItemPreset Get(int index);
        bool TryGet(int index, out ComponentItemPreset preset);
    }
}
```

- [ ] **Step 2: Implementation**

```csharp
using System.Collections.Generic;
using System.Linq;
using Data.Configs;
using UnityEngine;

namespace Infrastructure.Services.Carry
{
    /// <summary>
    /// Loads every ComponentItemPreset from Resources/Configs/Carry/Presets/, sorted by name
    /// for cross-machine determinism. Server and clients both load identical indices, so a
    /// SyncVar<int> on Carryable safely identifies a preset across the wire.
    /// </summary>
    public sealed class ComponentPresetRegistry : IComponentPresetRegistry
    {
        private readonly List<ComponentItemPreset> _presets;

        public ComponentPresetRegistry()
        {
            _presets = Resources.LoadAll<ComponentItemPreset>("Configs/Carry/Presets")
                .OrderBy(p => p.name, System.StringComparer.Ordinal)
                .ToList();
            if (_presets.Count == 0)
                Debug.LogError("[ComponentPresetRegistry] No presets found under Resources/Configs/Carry/Presets/.");
        }

        public int Count => _presets.Count;
        public ComponentItemPreset Get(int index) => _presets[index];
        public bool TryGet(int index, out ComponentItemPreset preset)
        {
            if (index < 0 || index >= _presets.Count) { preset = null; return false; }
            preset = _presets[index];
            return true;
        }
    }
}
```

- [ ] **Step 3: Bind in `ProjectInstaller`**

Add to `BindProviders` (or a new `BindCarry` method):
```csharp
Container.Bind<IComponentPresetRegistry>().To<ComponentPresetRegistry>().AsSingle();
```

- [ ] **Step 4: Recompile & verify** — 0 CS errors.

---

### Task 7: `Carryable.cs` NetworkBehaviour

**Files:**
- Create: `Assets/Scripts/Gameplay/World/Items/Carryable.cs`

- [ ] **Step 1: Write Carryable**

```csharp
using Data.Configs;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Infrastructure.Services.Carry;
using UnityEngine;
using Zenject;

namespace Gameplay.World.Items
{
    /// <summary>
    /// Marker + preset replication for a carryable physics item. Lives on ComponentItem.prefab.
    /// The actual carry mechanics are on PlayerCarry (server creates the ConfigurableJoint there).
    /// Carryable just holds the preset index (SyncVar) and replays ApplyPreset on every machine.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Carryable : NetworkBehaviour
    {
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private MeshCollider _collider;
        [SerializeField] private Rigidbody _rb;

        private readonly SyncVar<int> _presetIndex = new(-1);
        // Observability — joint state lives on the holders' PlayerCarry, not here.
        public readonly SyncVar<int> Holder1 = new(-1);
        public readonly SyncVar<int> Holder2 = new(-1);

        [Inject] private IComponentPresetRegistry _registry;

        public Rigidbody Body => _rb;

        private void Awake()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _presetIndex.OnChange += OnPresetIndexChanged;
        }

        private void OnDestroy()
        {
            _presetIndex.OnChange -= OnPresetIndexChanged;
        }

        /// <summary>Server-only — set after Spawn. Replicates and triggers ApplyPreset on every machine.</summary>
        public void ServerSetPreset(int index) => _presetIndex.Value = index;

        private void OnPresetIndexChanged(int prev, int next, bool asServer)
        {
            if (_registry == null || !_registry.TryGet(next, out var preset))
            {
                Debug.LogWarning($"[Carryable] Preset index {next} out of range (registry count {_registry?.Count}). Item will render as prefab default.", this);
                return;
            }
            ApplyPreset(preset);
        }

        public void ApplyPreset(ComponentItemPreset preset)
        {
            if (preset == null) return;
            if (_meshFilter != null) _meshFilter.sharedMesh = preset.Mesh;
            if (_renderer != null && preset.Material != null) _renderer.sharedMaterial = preset.Material;
            if (_collider != null)
            {
                _collider.sharedMesh = preset.Mesh;
                _collider.convex = true;
            }
            if (_rb != null) _rb.mass = preset.Mass;
            transform.localScale = preset.Scale;
        }

        /// <summary>Inspector tool — re-applies the current preset live (after a designer edits the SO).</summary>
        [ContextMenu("Apply Preset Live")]
        private void ApplyPresetLive()
        {
            if (_registry != null && _registry.TryGet(_presetIndex.Value, out var p))
                ApplyPreset(p);
        }
    }
}
```

- [ ] **Step 2: Recompile & verify** — 0 CS errors.

---

### Task 8: `ComponentItem.prefab`

Built via MCP (`manage_gameobject create` + `manage_components add` + `manage_prefabs create_from_gameobject`).

- [ ] **Step 1: Create a temporary scene GameObject named `ComponentItem`** with components in this order:
  1. `Rigidbody` — drag 0.3, angularDrag 0.5, useGravity true, interpolation Interpolate, collisionDetectionMode Continuous.
  2. `MeshFilter` — no mesh (preset will fill).
  3. `MeshRenderer` — default URP material is fine (preset may override).
  4. `MeshCollider` — convex true, sharedMesh null (preset will fill).
  5. `FishNet.Object.NetworkObject`.
  6. `FishNet.Component.Transforming.NetworkRigidbody` (or `RigidbodyChild` etc. — pick whichever ships in the project's FishNet).
  7. `Carryable` — wire `_meshFilter`, `_renderer`, `_collider`, `_rb` to the prefab's own components.

  Set GameObject layer to `Carryable`.

- [ ] **Step 2: Save as prefab** at `Assets/Prefabs/World/Items/ComponentItem.prefab` via `manage_prefabs action=create_from_gameobject`.

- [ ] **Step 3: Delete the temporary scene GameObject.**

- [ ] **Step 4: Register the prefab in `DefaultPrefabObjects.asset`** (FishNet's spawnable registry). FishNet typically auto-discovers prefabs with `NetworkObject` in `Resources` or under known folders; if our project uses an explicit list (it does — the file is in `Assets/`), the user may need to refresh it. **Note in summary**: after creating the prefab, ask user to right-click `DefaultPrefabObjects.asset → Refresh` if FishNet doesn't auto-register.

---

### Task 9: `IInputService` extension — `Interact` events

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Services/Input/IInputService.cs`
- Modify: `Assets/Scripts/Infrastructure/Services/Input/InputService.cs`

- [ ] **Step 1: Interface — add events**

```csharp
event Action InteractStarted;
event Action InteractCanceled;
```

- [ ] **Step 2: `InputService` — find + subscribe the action**

In `TryBindGeneratedControls`, alongside the existing `_jumpAction` block, find `_interactAction = gameplay.GetType().GetProperty("Interact")?.GetValue(gameplay) as InputAction;`. If null → log warning «add Interact action with `<Keyboard>/e` binding» (same shape as the Jump warning). Subscribe `performed → OnInteractPerformed`, `canceled → OnInteractCanceled`. Add `Enable()`/`Disable()`/`Dispose()` cleanup mirroring Jump.

```csharp
private void OnInteractPerformed(InputAction.CallbackContext _) => InteractStarted?.Invoke();
private void OnInteractCanceled(InputAction.CallbackContext _)  => InteractCanceled?.Invoke();
```

- [ ] **Step 3: Recompile & verify** — 0 CS errors. The console warning about missing `Interact` is expected until the user adds it to `.inputactions` (Task 14).

---

### Task 10: `PlayerCarry.cs` NetworkBehaviour

**Files:**
- Create: `Assets/Scripts/Gameplay/Player/Carry/PlayerCarry.cs`

- [ ] **Step 1: Write PlayerCarry**

```csharp
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Gameplay.Player.Camera;
using Gameplay.World.Items;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Input;
using UnityEngine;
using Zenject;

namespace Gameplay.Player.Carry
{
    /// <summary>
    /// Owner reads input + camera raycast; server creates/destroys the ConfigurableJoint
    /// that holds the item. Two-handed and throw inertia are emergent from physics.
    /// </summary>
    public sealed class PlayerCarry : NetworkBehaviour
    {
        [SerializeField] private Transform _handSocket;
        [SerializeField] private Rigidbody _handSocketRb;
        [SerializeField] private LayerMask _carryableMask;
        [SerializeField] private PlayerCameraRig _cameraRig;

        [Inject] private IInputService _input;
        [Inject] private IConfigDataProvider _configs;

        // Server-only state
        private ConfigurableJoint _activeJoint;
        private Carryable _heldItem;
        private bool _inputBound;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (base.IsOwner) BindInput();
        }
        public override void OnStopClient()
        {
            if (base.IsOwner) UnbindInput();
            base.OnStopClient();
        }
        public override void OnStopServer()
        {
            ForceRelease(); // disconnect mid-carry — clean up
            base.OnStopServer();
        }
        private void OnDestroy() => UnbindInput();

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
            var cam = _cameraRig != null ? _cameraRig.Camera : null;
            if (cam == null) return;
            var carry = _configs.Carry;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit,
                    carry.MaxReach, _carryableMask, QueryTriggerInteraction.Ignore))
                return;
            var carryable = hit.collider.GetComponentInParent<Carryable>();
            if (carryable == null) return;
            RequestGrab(carryable.NetworkObject, hit.point);
        }

        private void OnInteractCanceled() => RequestRelease();

        [ServerRpc]
        private void RequestGrab(NetworkObject itemNob, Vector3 worldHitPoint)
        {
            if (itemNob == null) return;
            if (_activeJoint != null) return; // hold-to-grip; reject re-grab without release
            var carryable = itemNob.GetComponent<Carryable>();
            if (carryable == null || carryable.Body == null) return;

            var carry = _configs.Carry;
            if (Vector3.Distance(transform.position, worldHitPoint) > carry.MaxReach * carry.ServerReachTolerance) return;

            var joint = _handSocket.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = carryable.Body;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = carryable.transform.InverseTransformPoint(worldHitPoint);

            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
            var lim = joint.linearLimit; lim.limit = carry.JointLinearLimit; joint.linearLimit = lim;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;

            var lin = new JointDrive { positionSpring = carry.JointLinearSpring, positionDamper = carry.JointLinearDamper, maximumForce = carry.JointMaxForce };
            joint.xDrive = joint.yDrive = joint.zDrive = lin;
            var ang = new JointDrive { positionSpring = carry.JointAngularSpring, positionDamper = carry.JointAngularDamper, maximumForce = float.MaxValue };
            joint.slerpDrive = ang;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.enablePreprocessing = false;

            _activeJoint = joint;
            _heldItem = carryable;
            // Mark holder slot for observability
            if (carryable.Holder1.Value == -1) carryable.Holder1.Value = (int)base.OwnerId;
            else if (carryable.Holder2.Value == -1) carryable.Holder2.Value = (int)base.OwnerId;
        }

        [ServerRpc]
        private void RequestRelease() => ForceRelease();

        private void ForceRelease()
        {
            if (_activeJoint == null) return;
            var carry = _configs?.Carry;
            var rb = _activeJoint.connectedBody;
            UnityEngine.Object.Destroy(_activeJoint);
            _activeJoint = null;
            if (rb != null && carry != null && carry.ReleaseVelocityBoost != 1f)
                rb.linearVelocity *= carry.ReleaseVelocityBoost;
            if (_heldItem != null)
            {
                if (_heldItem.Holder1.Value == (int)base.OwnerId) _heldItem.Holder1.Value = -1;
                else if (_heldItem.Holder2.Value == (int)base.OwnerId) _heldItem.Holder2.Value = -1;
                _heldItem = null;
            }
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
            if (carry.DebugDrawGrab && _activeJoint != null && _heldItem != null)
            {
                Gizmos.color = Color.red;
                var world = _heldItem.transform.TransformPoint(_activeJoint.connectedAnchor);
                Gizmos.DrawSphere(world, 0.05f);
                Gizmos.DrawLine(_handSocket.position, world);
            }
        }

        private void OnGUI()
        {
            var carry = _configs?.Carry;
            if (carry == null || !carry.DebugOverlay) return;
            if (!base.IsOwner) return;
            var rb = _activeJoint?.connectedBody;
            var label = rb != null
                ? $"Holding: {rb.name} | Mass: {rb.mass:F1} | |v|: {rb.linearVelocity.magnitude:F2}"
                : "Holding: —";
            GUI.Label(new Rect(10, 10, 600, 24), label);
        }
#endif
    }
}
```

- [ ] **Step 2: Recompile & verify** — 0 CS errors. (`_cameraRig` will resolve once the prefab is updated in Task 11; field is `[SerializeField]` so no Inject-binding needed.)

> **Subagent self-check after Task 10:** any tunable not in `CarryConfig`? Reach, drives, motion modes, boost, all debug toggles → yes, all there. Any magic number in PlayerCarry? Only the Gizmos.DrawSphere radius (0.05f) — cosmetic, fine.

---

### Task 11: Player.prefab — add `RightHandSocket` + `PlayerCarry`

Done via MCP (`manage_prefabs open_prefab_stage`, `manage_gameobject create` for child, `manage_components add`, save).

- [ ] **Step 1: Open prefab stage** for `Assets/Prefabs/Player.prefab`.
- [ ] **Step 2: Create child GameObject `RightHandSocket`** under the Player root, local position `(0.4, 1.3, 0.6)`. Add components: `Rigidbody` (`isKinematic = true`, `useGravity = false`). No collider.
- [ ] **Step 3: Add `PlayerCarry`** component on the Player root.
- [ ] **Step 4: Wire serialized refs** on PlayerCarry:
  - `_handSocket` → the `RightHandSocket` Transform.
  - `_handSocketRb` → its Rigidbody.
  - `_carryableMask` → the `Carryable` layer.
  - `_cameraRig` → the existing PlayerCameraRig on the player.
- [ ] **Step 5: Save prefab stage**, close.
- [ ] **Step 6: Verify** via `manage_prefabs get_hierarchy` — Player has `RightHandSocket` child and `PlayerCarry` component.

---

### Task 12: `IComponentSpawnService` + `ComponentSpawnService`

**Files:**
- Create: `Assets/Scripts/Infrastructure/Services/Spawn/IComponentSpawnService.cs`
- Create: `Assets/Scripts/Infrastructure/Services/Spawn/ComponentSpawnService.cs`

- [ ] **Step 1: Interface**

```csharp
namespace Infrastructure.Services.Spawn
{
    public interface IComponentSpawnService { }
}
```
(Marker only — lifecycle via `IInitializable`/`IDisposable`; consumers don't call into it.)

- [ ] **Step 2: Implementation**

```csharp
using System;
using FishNet.Connection;
using Gameplay.World.Items;
using Infrastructure.Providers.Configs;
using Infrastructure.Services.Carry;
using Infrastructure.Services.Network;
using Infrastructure.Services.World;
using Signals;
using UnityEngine;
using Zenject;

namespace Infrastructure.Services.Spawn
{
    public sealed class ComponentSpawnService : IComponentSpawnService, IInitializable, IDisposable
    {
        private readonly INetworkService _network;
        private readonly INetworkSpawnService _spawner;
        private readonly IWorldGenerationService _worldGen;
        private readonly IConfigDataProvider _configs;
        private readonly IWorldSeedProvider _seedProvider;
        private readonly SignalBus _signalBus;
        private readonly IComponentPresetRegistry _presets;
        private readonly GameObject _componentItemPrefab;

        public ComponentSpawnService(INetworkService network, INetworkSpawnService spawner,
            IWorldGenerationService worldGen, IConfigDataProvider configs,
            IWorldSeedProvider seedProvider, SignalBus signalBus,
            IComponentPresetRegistry presets, GameObject componentItemPrefab)
        {
            _network = network; _spawner = spawner; _worldGen = worldGen; _configs = configs;
            _seedProvider = seedProvider; _signalBus = signalBus; _presets = presets;
            _componentItemPrefab = componentItemPrefab;
        }

        public void Initialize() => _signalBus.Subscribe<WorldGeneratedSignal>(OnWorldGenerated);
        public void Dispose()    => _signalBus.TryUnsubscribe<WorldGeneratedSignal>(OnWorldGenerated);

        private void OnWorldGenerated(WorldGeneratedSignal _)
        {
            if (_network == null || !_network.IsServer) return;
            if (_componentItemPrefab == null) { Debug.LogError("[ComponentSpawnService] ComponentItem prefab not assigned in GameSceneInstaller."); return; }
            if (_presets.Count == 0) { Debug.LogWarning("[ComponentSpawnService] No presets — skipping spawn."); return; }

            var anchors = _worldGen.Result?.ComponentAnchors;
            if (anchors == null) return;
            float chance = _configs.World.ComponentSpawnChance;

            for (int i = 0; i < anchors.Count; i++)
            {
                var rng = new DeterministicRandom(DeterministicRandom.Mix(_seedProvider.Seed, 404 + i));
                if (rng.NextFloat() > chance) continue;
                int idx = Mathf.Min(_presets.Count - 1, (int)(rng.NextFloat() * _presets.Count));

                var go = _spawner.SpawnNetworked(_componentItemPrefab,
                    anchors[i].Position + Vector3.up * 0.5f, Quaternion.identity, owner: null);
                if (go == null) continue;
                var carryable = go.GetComponent<Carryable>();
                if (carryable != null) carryable.ServerSetPreset(idx);
            }
        }
    }
}
```

- [ ] **Step 3: Recompile & verify** — 0 CS errors.

---

### Task 13: `GameSceneInstaller` — bind spawn service + prefab ref

**Files:**
- Modify: `Assets/Scripts/Infrastructure/Installers/GameSceneInstaller.cs`

- [ ] **Step 1: Add SerializeField + binding**

```csharp
[Header("Components")]
[SerializeField] private GameObject _componentItemPrefab;
```

In `InstallBindings`, after the existing `BindSpawn`/`BindWorld`:
```csharp
if (_componentItemPrefab != null)
    Container.BindInterfacesAndSelfTo<ComponentSpawnService>().AsSingle().WithArguments(_componentItemPrefab);
else
    Debug.LogWarning("[GameSceneInstaller] ComponentItem prefab not assigned; components won't spawn.", this);
```

- [ ] **Step 2: Assign prefab ref in `Game.unity`** via MCP `manage_components set_property` on `SceneContext`'s `GameSceneInstaller` → `_componentItemPrefab` = the `ComponentItem.prefab` asset path.

- [ ] **Step 3: Save scene** + recompile + verify 0 CS errors.

---

### Task 14: Editor tools — Spawn / Despawn menu

**Files:**
- Create: `Assets/Scripts/EditorTools/CarryTestMenu.cs`

(EditorTools folder already exists per CLAUDE.md.)

- [ ] **Step 1: Write the menu**

```csharp
#if UNITY_EDITOR
using FishNet.Object;
using Gameplay.World.Items;
using Infrastructure.Services.Carry;
using UnityEditor;
using UnityEngine;
using Zenject;

namespace EditorTools
{
    /// <summary>
    /// Play-mode-only test helpers. Spawn one of every preset in front of the local
    /// host player so a tester can iterate on tuning without waiting for world gen.
    /// </summary>
    public static class CarryTestMenu
    {
        [MenuItem("Tools/Co-op/Carry/Spawn Test Items", false, 100)]
        private static void SpawnTestItems()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[CarryTestMenu] Enter Play Mode first."); return; }
            var ctx = ProjectContext.Instance;
            if (ctx == null) { Debug.LogError("[CarryTestMenu] ProjectContext not initialised."); return; }
            var spawner = ctx.Container.TryResolve<Infrastructure.Services.Spawn.INetworkSpawnService>();
            var registry = ctx.Container.TryResolve<IComponentPresetRegistry>();
            var network  = ctx.Container.TryResolve<Infrastructure.Services.Network.INetworkService>();
            if (spawner == null || registry == null || network == null || !network.IsServer)
            { Debug.LogError("[CarryTestMenu] Run on the host (server) editor."); return; }

            // Locate ComponentItem prefab via the installer's serialized ref.
            var installer = UnityEngine.Object.FindFirstObjectByType<Infrastructure.Installers.GameSceneInstaller>();
            var prefabField = typeof(Infrastructure.Installers.GameSceneInstaller)
                .GetField("_componentItemPrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var prefab = prefabField?.GetValue(installer) as GameObject;
            if (prefab == null) { Debug.LogError("[CarryTestMenu] ComponentItem prefab missing in installer."); return; }

            // Spawn one of each preset 1.5m in front of the host's main camera.
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[CarryTestMenu] No main camera."); return; }
            var basePos = cam.transform.position + cam.transform.forward * 1.5f;

            for (int i = 0; i < registry.Count; i++)
            {
                var go = spawner.SpawnNetworked(prefab, basePos + Vector3.right * (i * 0.7f), Quaternion.identity, owner: null);
                var carryable = go != null ? go.GetComponent<Carryable>() : null;
                if (carryable != null) carryable.ServerSetPreset(i);
            }
            Debug.Log($"[CarryTestMenu] Spawned {registry.Count} test items.");
        }

        [MenuItem("Tools/Co-op/Carry/Despawn All Items", false, 101)]
        private static void DespawnAllItems()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[CarryTestMenu] Enter Play Mode first."); return; }
            var network = ProjectContext.Instance?.Container.TryResolve<Infrastructure.Services.Network.INetworkService>();
            if (network == null || !network.IsServer) { Debug.LogError("[CarryTestMenu] Run on the host."); return; }
            int n = 0;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Carryable>(FindObjectsSortMode.None))
            {
                var nob = c.GetComponent<NetworkObject>();
                if (nob == null) continue;
                network.NetworkManager.ServerManager.Despawn(nob.gameObject);
                n++;
            }
            Debug.Log($"[CarryTestMenu] Despawned {n} items.");
        }
    }
}
#endif
```

- [ ] **Step 2: Recompile & verify** — 0 CS errors.

> **Subagent self-check:** any additional tool that would simplify play-testing? — `ContextMenu("Apply Preset Live")` on Carryable already covers live-tune. `[ContextMenu("Reset to defaults")]` on CarryConfig already there. Debug gizmo + overlay on PlayerCarry already there. ✓ enough for MVP.

---

### Task 15: User-side manual steps (guided)

Hand off to the user with clear, exact steps:

- [ ] **Step 1: Add `Interact` action to `Assets/Settings/Input/PlayerControls.inputactions`**
  - Open the asset in Unity.
  - In `Gameplay` action map → `+ → Add Action` → name `Interact`, Type = `Button`.
  - On the Interact action: `+ → Add Binding` → Path = `<Keyboard>/e`.
  - Save the asset → it regenerates `PlayerControls.cs` automatically.

- [ ] **Step 2: Assign materials to presets (optional, placeholder visuals)**
  - For each preset under `Assets/Resources/Configs/Carry/Presets/`: select asset → set `Material` field to a URP/Lit material (different colour per preset for visibility). If left null, items render with prefab default material.

- [ ] **Step 3: Verify Game.unity wiring**
  - `SceneContext → GameSceneInstaller → _componentItemPrefab` is set to `Assets/Prefabs/World/Items/ComponentItem.prefab` (Task 13 step 2 should have done it via MCP, but eye-check).

- [ ] **Step 4: Play-mode QA**
  - **Bootstrap → Play → Host:** expect items spawned at component anchors (look for them around the Base anchor).
  - **Tools → Co-op → Carry → Spawn Test Items** (alternative): spawns one of each preset directly in front of you.
  - **Aim at item + hold E:** item lifts to your hand at the exact point you aimed.
  - **Move + release E:** item flies off with inertia.
  - **Heavy item (HeavyLong, mass 30):** can't lift solo — drags. Two players holding E together → lifts.
  - **Debug toggles** in `CarryConfig.asset`: flip `DebugDrawRaycast`, `DebugDrawGrab`, `DebugOverlay` to see visualisations.

---

### Task 16: Final compile + console sweep

- [ ] **Step 1:** `refresh_unity(wait_for_ready=false)` → poll `editor/state` until ready.
- [ ] **Step 2:** `read_console filter_text="error CS"` → expect 0.
- [ ] **Step 3:** `read_console types=["warning"] filter_text="InputService"` — expect a single `Interact action not found` warning **only** before the user does Task 15 step 1.

---

## Done criteria

- Compiles clean.
- Items spawn at procgen anchors with `ComponentSpawnChance` rolling.
- Aim + Hold E grabs at the hit point; release drops with inertia.
- Heavy (30 kg) needs two joints to lift; lighter masses lift solo.
- `Tools/Co-op/Carry/Spawn Test Items` works in play mode and gives instant test fodder.
- All tuning knobs (drives, reach, masses, debug flags) editable in CarryConfig + presets — no recompile.
- No HUD, no counter, no delivery — by design.
