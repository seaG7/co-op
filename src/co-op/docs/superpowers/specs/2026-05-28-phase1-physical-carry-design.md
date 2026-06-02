# Phase 1 — Physical Carry MVP

**Goal:** implement the GDD's central «физика переноса» primitive. Players approach a procedurally-spawned object, aim at it via the camera-centered crosshair, hold **E** to grab it at the exact hit point, and physically carry / drag / throw it. Two players can grab the same object at different points; heavy objects emerge as "two-handed" through physics, not gating. Releasing the button drops the object with its accumulated inertia.

No inventory, no slots, no HUD counters, no delivery tracking — the GDD's «всё руками, никакой магии» ethos. Counters/delivery come later, only when components are physically connected to the weapon (next milestone).

## Architecture

Three primitives:

- **`Carryable`** (NetworkBehaviour on each item) — marker + replicates the preset choice + observability SyncVars. Layer `Carryable`.
- **`PlayerCarry`** (NetworkBehaviour on Player.prefab) — owner reads input and raycasts from camera; server creates/destroys the `ConfigurableJoint`.
- **`ComponentSpawnService`** (server-only `IInitializable`) — on `WorldGeneratedSignal` rolls per-anchor probability and spawns the generic `ComponentItem.prefab` configured by a `ComponentItemPreset`.

**Authority model:** server-authoritative physics. Items are `NetworkObject` + FishNet `NetworkRigidbody` — server runs the Rigidbody/joint sim, all clients replicate. Owner input is forwarded via `ServerRpc`. Owner-side may see a small input→render lag for carried items but the carry feel is correct for LAN/low-latency co-op.

**Tuning lives in configs, not code.** A single `CarryConfig` SO (read via `IConfigDataProvider.Carry`) holds *every* number a designer or tester might want to tweak: joint drives, max reach, release boost. Per-item variation lives in **`ComponentItemPreset` SOs** that the spawn service picks from. The runtime prefab is *one* `ComponentItem.prefab` — presets define its mesh / material / mass. This means adding a new item type = create one preset asset; tweaking feel = edit one SO; no prefab/code changes.

**The carry mechanism** is a `ConfigurableJoint` per grab:
- Lives on the player's `RightHandSocket` (a kinematic-Rigidbody child of the player).
- `connectedBody` = item's Rigidbody.
- `anchor` = `Vector3.zero` (the socket's own pivot).
- `connectedAnchor` = `item.transform.InverseTransformPoint(worldHitPoint)` — the **local point on the item that the raycast hit**. The joint thus pulls *that exact point on the item* toward the hand position; the rest of the item dangles/swings around it.
- Drive params (spring/damper/maxForce, linear & angular motion modes) come from `CarryConfig`.

**Two-handed = pure physics emergence.** Both players can grab the same item; each creates its own joint. Heavy items (high `mass`) require the combined force of two joint drives to overcome gravity. No `MinHands` field, no UI prompt — physics tells the player «один не могу, нужен второй».

**Throw with inertia = pure physics emergence.** While carrying, the kinematic hand-socket motion pulls the item's Rigidbody via the joint, accumulating `linearVelocity` / `angularVelocity` in the item. On release the joint is destroyed; the Rigidbody keeps its velocity → the item flies off in the direction it was being dragged. Optional `ReleaseVelocityBoost` (default 1.0 — pure physics; ≥1 for snappier feel) multiplies velocity on release.

## Configs

### `CarryConfig` — `Data/Configs/CarryConfig.cs` (new SO)
Loaded once via `IConfigDataProvider.Carry` from `Resources/Configs/Carry/CarryConfig.asset`.

Fields (every one tunable from the inspector with sensible defaults):
- `float MaxReach = 1.5f` — raycast distance.
- `float ServerReachTolerance = 1.2f` — multiplier on MaxReach for the server-side validation (latency forgiveness).
- `float JointLinearSpring = 8000f`, `float JointLinearDamper = 200f`, `float JointMaxForce = 200f` — linear drive (lift power).
- `float JointAngularSpring = 500f`, `float JointAngularDamper = 50f` — angular drive (sway feel).
- `float JointLinearLimit = 0.05f` — soft positional limit; small ≈ rigid, larger ≈ rubbery.
- `float ReleaseVelocityBoost = 1.0f` — ≥1 for snappier throw, 1 = pure physics.
- `bool DebugDrawRaycast = false`, `bool DebugDrawGrab = false`, `bool DebugOverlay = false` — runtime toggles for the gizmos / on-screen debug.

`[ContextMenu("Reset to defaults")]` button restores the above defaults — gives the tester a one-click escape from broken tuning.

### `ComponentItemPreset` — `Data/Configs/ComponentItemPreset.cs` (new SO)
A *variant* of `ComponentItem.prefab`. Fields:
- `Mesh Mesh` — placeholder primitive shape (cube, sphere, etc. — designer can assign Unity built-ins).
- `Material Material` — distinct colour per preset (placeholder visuals).
- `float Mass = 2f` — Rigidbody mass; gates one-handed vs two-handed naturally.
- `Vector3 Scale = Vector3.one` — uniform scale of the item.

Spawned items get all of these applied at runtime (see `Carryable.ApplyPreset`). Adding a new item variant = create a new preset asset. No code, no prefab.

Three preset assets shipped under `Resources/Configs/Carry/Presets/`:

| Preset                       | Mesh    | Mass | Scale          | Behavior              |
|------------------------------|---------|------|----------------|-----------------------|
| `Preset_LightCube.asset`     | Cube    | 2    | `(0.4,0.4,0.4)`| snappy one-handed     |
| `Preset_MediumSphere.asset`  | Sphere  | 8    | `(0.5,0.5,0.5)`| one-handed but slower |
| `Preset_HeavyLong.asset`     | Cube    | 30   | `(1.5,0.3,0.3)`| two-handed only       |

## Components

### `Carryable` — `Gameplay/World/Items/Carryable.cs`
NetworkBehaviour. Mostly a marker:
- `[SerializeField] MeshFilter _meshFilter`, `MeshRenderer _renderer`, `MeshCollider _collider`, `Rigidbody _rb` (wired in the prefab).
- `SyncVar<int> _presetIndex = -1` — server sets it on spawn; clients apply preset on `OnChange`.
- `SyncVar<int> Holder1, Holder2 = -1` — observability (ClientIds of current holders). Not load-bearing — joints live on the holders' `PlayerCarry`.
- `void ApplyPreset(ComponentItemPreset p)`: sets mesh on `_meshFilter` and `_collider` (convex), assigns material, sets `_rb.mass`, sets `transform.localScale`. Called both on server (right after spawn) and on each client (via SyncVar.OnChange).
- Layer `Carryable` (assigned in the prefab and enforced at spawn).

The preset registry — `IComponentPresetRegistry` (loaded at boot from Resources, indexed list) — lets the SyncVar carry an int and both sides resolve to the same preset.

### `PlayerCarry` — `Gameplay/Player/Carry/PlayerCarry.cs`
NetworkBehaviour on Player.prefab.

Serialized refs / injected:
- `[SerializeField] Transform _handSocket` — `RightHandSocket` child of player.
- `[SerializeField] LayerMask _carryableMask` — set to layer `Carryable`.
- `[Inject] IInputService _input`.
- `[Inject] IConfigDataProvider _configs` (read `_configs.Carry` per grab — designers can hot-tweak the SO between grabs).
- Camera ref: from `PlayerCameraRig.Camera` on the player (already present).

Server-side state: `ConfigurableJoint _activeJoint`, `Carryable _heldItem`.

Lifecycle:
- `OnStartClient` (owner): subscribe `_input.InteractStarted += OnInteractStarted; _input.InteractCanceled += OnInteractCanceled`.
- `OnStopClient` (owner): unsubscribe. Server-side `OnStopServer` also force-releases any active joint (player disconnect mid-carry).

Owner flow on Interact press:
1. If already holding → ignore (Hold-to-grip; another press doesn't toggle).
2. `Physics.Raycast(camera.position, camera.forward, _configs.Carry.MaxReach, _carryableMask)`.
3. If hit on a Carryable: `[ServerRpc] RequestGrab(carryable.NetworkObject, hit.point)`.

Owner flow on Interact release: `[ServerRpc] RequestRelease()`.

Server `RequestGrab(NetworkObject itemNob, Vector3 worldHitPoint)`:
- Validate: itemNob != null, has Rigidbody, has Carryable, `_activeJoint == null` on this PlayerCarry, `Vector3.Distance(player.position, worldHitPoint) ≤ _configs.Carry.MaxReach * _configs.Carry.ServerReachTolerance`.
- `AddComponent<ConfigurableJoint>()` on `_handSocket.gameObject`:
  - `connectedBody = item.Rigidbody`.
  - `anchor = Vector3.zero`.
  - `connectedAnchor = item.transform.InverseTransformPoint(worldHitPoint)`.
  - linear `x/y/zMotion = Limited`, `linearLimit.limit = _configs.Carry.JointLinearLimit`.
  - angular `x/y/zMotion = Free`.
  - `xDrive/yDrive/zDrive` ← spring/damper/maxForce from CarryConfig.
  - `slerpDrive` angular ← spring/damper from CarryConfig.
  - `rotationDriveMode = Slerp`.
  - `enablePreprocessing = false`.
- Set `_heldItem = carryable`; set the appropriate holder slot.

Server `RequestRelease()`:
- If `_activeJoint == null` → ignore.
- `Destroy(_activeJoint)`. Item Rigidbody keeps its accumulated velocity.
- Apply `linearVelocity *= _configs.Carry.ReleaseVelocityBoost` (boost ≥1; 1 = pure physics).
- Clear our holder slot on `_heldItem`; `_heldItem = null`.

### `IComponentSpawnService` + `ComponentSpawnService` — `Infrastructure/Services/Spawn/`
Server-only `IInitializable, IDisposable`. Constructor:
```
(INetworkService network,
 INetworkSpawnService spawner,
 IWorldGenerationService worldGen,
 IConfigDataProvider configs,
 IWorldSeedProvider seedProvider,
 SignalBus signalBus,
 IComponentPresetRegistry presets,
 GameObject componentItemPrefab)
```

`Initialize`: subscribes `signalBus.Subscribe<WorldGeneratedSignal>(OnWorldGenerated)`.
`Dispose`: unsubscribes.

`OnWorldGenerated`:
- If `!_network.IsServer` → return.
- For each anchor index `i` in `_worldGen.Result.ComponentAnchors`:
  - `rng = new DeterministicRandom(DeterministicRandom.Mix(_seedProvider.Seed, 404 + i));`
  - If `rng.NextFloat() > _configs.World.ComponentSpawnChance` → skip.
  - `int idx = (int)(rng.NextFloat() * _presets.Count);`
  - Spawn via `INetworkSpawnService.SpawnNetworked(_componentItemPrefab, anchor.Position + Vector3.up * 0.5f, identity, owner: null)`.
  - On the spawned `Carryable`: set `_presetIndex.Value = idx` → SyncVar replicates → both server and clients invoke `ApplyPreset(_presets[idx])`.

### `IComponentPresetRegistry` + `ComponentPresetRegistry` — `Infrastructure/Services/Carry/`
Singleton service. On boot, loads all `ComponentItemPreset` assets from `Resources/Configs/Carry/Presets/` into a stable-order list (sorted by name for cross-machine determinism). Bound in `ProjectInstaller` (whole-app scope; both server and clients need it for SyncVar lookup).

### `ComponentItem.prefab` — `Assets/Prefabs/World/Items/`
Single generic carryable prefab.
- Root: `NetworkObject` + `NetworkRigidbody` (FishNet) + `Rigidbody` (default mass 1 — overridden by preset; `drag = 0.3`, `angularDrag = 0.5`, `useGravity = true`, `interpolation = Interpolate`, `collisionDetectionMode = Continuous`) + `MeshFilter` + `MeshRenderer` + `MeshCollider` (convex = true; mesh overridden by preset) + `Carryable`.
- Layer = `Carryable`.
- Mesh / material / mass / scale are blank in the prefab — `Carryable.ApplyPreset` fills them from the SyncVar'd preset.

### `RightHandSocket` (under Player.prefab)
Empty child GameObject. Local position ≈ `(0.4, 1.3, 0.6)` (placeholder, body-relative). Rigidbody `isKinematic = true`, no collider. **Fixed for now** — later we'll add a small `HandSocketTuner` MB so the gamedev can drag it in the inspector and live-preview at runtime; for now a comment marks the placeholder.

### `WorldGenConfig` (existing — extend)
Add `[Range(0,1)] float ComponentSpawnChance = 0.75f` (≈ 4-5 items per round on average across 6 anchors).

### `GameSceneInstaller` (existing — extend)
- `[SerializeField] GameObject _componentItemPrefab` — single prefab ref.
- Bind `IComponentSpawnService` with `WithArguments(_componentItemPrefab)`.

### `ProjectInstaller` (existing — extend)
- Bind `IComponentPresetRegistry` `AsSingle()` and `IConfigDataProvider`'s loader pipeline updated to include CarryConfig.

### Input
`IInputService` (extended): `event Action InteractStarted`, `event Action InteractCanceled`.
`InputService.TryBindGeneratedControls`: locate `Gameplay/Interact` action via reflection; subscribe `performed → InteractStarted`, `canceled → InteractCanceled`.
User-side editor work: in `Assets/Settings/Input/PlayerControls.inputactions` add action `Interact` (Button) + binding `<Keyboard>/e`.

### Project layer
Add layer `Carryable` (next free slot, e.g. 8) via Tags & Layers manager.

## Editor tooling

Tests and tuning are easier if the right buttons exist. Built now to pay off through the rest of development.

### `[Tools/Co-op/Carry/Spawn Test Items]` menu (`Assets/Scripts/EditorTools/CarryTestMenu.cs`)
In Play Mode only, spawns one of each preset in front of the **server** player (skips client/non-server editor with a clear log). Lets a tester verify pickup feel without waiting for `WorldGeneratedSignal` or walking across the terrain. Greyed out outside Play Mode (or runs `Debug.LogWarning` and returns).

### `[Tools/Co-op/Carry/Despawn All Items]` menu
Mirrors the above — clears all `Carryable` NetworkObjects from the scene. Useful after iterating tuning.

### Debug gizmo on `PlayerCarry` (`OnDrawGizmosSelected`)
When `_configs.Carry.DebugDrawRaycast` is true, draw a line from camera forward `MaxReach` long. When `DebugDrawGrab` is true and `_activeJoint != null`, draw a red sphere at the world `connectedAnchor` on the held item — the exact grab point — and a line from hand socket to that point.

### Debug overlay on `PlayerCarry` (`OnGUI`)
When `_configs.Carry.DebugOverlay` is true, draws a top-left text block: `Holding: {item.name} | Mass: {rb.mass:F1} | |v|: {rb.velocity.magnitude:F2} m/s | Holders: {h1},{h2}`. Off by default — flip in the SO to enable. Off in builds.

### `[ContextMenu]` on `CarryConfig`
`Reset To Defaults` — one click puts every value back to the recommended baseline (the constants in this spec).

### `[ContextMenu]` on `Carryable`
`Apply Preset Live` — for an item already in the scene, re-runs `ApplyPreset(_presets[_presetIndex])`. So a designer can tweak a preset SO mid-play, right-click → Apply, and see the change without despawning.

### Future-friendly
A note in `RightHandSocket`'s component comment: «placeholder for capsule; replace with a `HandSocketTuner` MB that exposes localPosition + arc length sliders when the player gets a real rig».

## Data flow

```
Server boot → WorldGenerationService.GenerateAsync → fires WorldGeneratedSignal
  ↓
ComponentSpawnService.OnWorldGenerated (server, IsServer-gated)
  ↓
per anchor: deterministic rng → roll vs ComponentSpawnChance → pick preset index
  ↓
INetworkSpawnService.SpawnNetworked(ComponentItem.prefab) → server sets Carryable._presetIndex.Value
  ↓
SyncVar replicates → Carryable.OnPresetIndexChanged on every machine → ApplyPreset(_presets[idx])
  ↓
NetworkRigidbody replicates physics from server to clients

Owner E press → InputService.InteractStarted → PlayerCarry.OnInteractStarted
  → Physics.Raycast from camera (centre-of-screen) along forward, MaxReach, _carryableMask
  → hit a Carryable? → RequestGrab(carryableNob, hit.point) [ServerRpc]
  ↓
Server validates + creates ConfigurableJoint on _handSocket using CarryConfig drives
  - anchor = 0; connectedBody = item Rb; connectedAnchor = item.InverseTransformPoint(hit.point)
  ↓
Hand socket follows player → joint pulls item → item accelerates → NetworkRigidbody replicates

Owner E release → InteractCanceled → RequestRelease [ServerRpc]
  → Destroy(joint) → Rigidbody keeps velocity ×ReleaseVelocityBoost → natural fall/throw

Two players grab same item → two PlayerCarry instances each create their own joint → drives sum → can lift heavy items
```

## Error handling

- Raycast hits nothing / wrong layer → owner returns early; no RPC.
- Server: `itemNob == null` (despawned in flight) → log + return.
- Server: distance > `MaxReach * ServerReachTolerance` → reject.
- Server: `_activeJoint != null` and a new RequestGrab arrives → reject (Hold-to-grip).
- Server: RequestRelease with no joint → ignore.
- Player disconnects mid-carry → server-side `OnStopServer` destroys joint (Rigidbody keeps velocity).
- Item despawned mid-carry (future weapon-assembly): `Carryable.OnStopServer` notifies any active holders to release. Outside MVP scope but flagged with a TODO.
- Heavy item, one hand: joint exists but `JointMaxForce / mass < g` → drags rather than lifts. *Correct behavior, not an error.*
- Preset index out of range (registry mismatch between machines): log error and skip ApplyPreset; item renders as the prefab default (empty mesh — visible bug, easy to diagnose).

## Tuning targets

Calibrated against `Physics.gravity = (0,-9.81,0)` (Unity default; we haven't changed it):

| Mass (kg) | One joint force / mass | vs g (9.81) | Behavior              |
|-----------|------------------------|-------------|-----------------------|
| 2 (Light) | 200 / 2 = 100          | ≫ g         | snappy one-handed     |
| 8 (Med)   | 200 / 8 = 25           | > g         | one-handed but slower |
| 30 (Heavy)| 200 / 30 ≈ 6.7         | < g         | drags on ground only  |
| 30 + two joints | 400 / 30 ≈ 13.3  | > g         | liftable              |

All these values live in `CarryConfig.asset` + preset assets — no code edits needed to retune.

## Networking notes

- `NetworkRigidbody` handles position/rotation/velocity sync from server.
- Joint lives only on server; clients see its *effect* (item Rigidbody state) via NetworkRigidbody.
- `RequestGrab` carries `Vector3 worldHitPoint`; server converts to local on the *server's* item transform.
- Owner-side latency: item replicates ~1 RTT behind — acceptable for LAN/low-latency. Ownership transfer for carried items is a future optimisation, not solved now.

## Determinism

Same seed → same anchors → same probability rolls → same preset picks (server). Server's spawn replicates authoritatively via NetworkObject. Salt `404 + i` per-anchor ensures adjacent rolls don't correlate.

## Out of scope (this milestone)

- HUD crosshair (user adds an Image themselves) and «Press E» hints.
- Counter / delivery — components don't count until physically connected to a future weapon (next milestone).
- Base / Source as physical objects (anchors visible via `WorldAnchor` gizmos for debug).
- Fragile breakage on impact.
- Weight-affects-walk-speed.
- Special "uncomfortable" component property.
- Scouts / enemies / waves.
- Animations / IK / hand poses.
- Real meshes / glow shader.
- Two-handed gating via UI (we use physics emergence).

## Testing notes

Manual play-mode QA via either: WorldGeneratedSignal → spawn cycle, **or** `Tools/Co-op/Carry/Spawn Test Items` to skip world gen during tuning.

- Solo carry: Light & Medium pick up cleanly; Heavy only drags.
- Two-handed lift: in ParrelSync second editor, both players hold E on Heavy → it lifts.
- Throw inertia: swing camera fast → release → item flies tangentially.
- Grab point: aim at corner of Heavy → it hangs from that corner, not centre.
- Live tuning: edit `CarryConfig.asset` in Play Mode, next grab uses new values; right-click a `Carryable` → `Apply Preset Live` to refresh in place.

Automated tests (EditMode): deferred per E13 (production asmdef refactor).

## Files

**Create:**
- `Assets/Scripts/Data/Configs/CarryConfig.cs`
- `Assets/Scripts/Data/Configs/ComponentItemPreset.cs`
- `Assets/Scripts/Data/Paths/ConfigPaths.cs` (extend, new `CARRY_CONFIG_PATH` const).
- `Assets/Scripts/Gameplay/World/Items/Carryable.cs`
- `Assets/Scripts/Gameplay/Player/Carry/PlayerCarry.cs`
- `Assets/Scripts/Infrastructure/Services/Spawn/IComponentSpawnService.cs`
- `Assets/Scripts/Infrastructure/Services/Spawn/ComponentSpawnService.cs`
- `Assets/Scripts/Infrastructure/Services/Carry/IComponentPresetRegistry.cs`
- `Assets/Scripts/Infrastructure/Services/Carry/ComponentPresetRegistry.cs`
- `Assets/Scripts/EditorTools/CarryTestMenu.cs` (editor-only — under `#if UNITY_EDITOR` or `Editor/` folder).
- `Assets/Prefabs/World/Items/ComponentItem.prefab`
- `Assets/Resources/Configs/Carry/CarryConfig.asset`
- `Assets/Resources/Configs/Carry/Presets/Preset_LightCube.asset`
- `Assets/Resources/Configs/Carry/Presets/Preset_MediumSphere.asset`
- `Assets/Resources/Configs/Carry/Presets/Preset_HeavyLong.asset`

**Modify:**
- `Assets/Prefabs/Player.prefab` — add `RightHandSocket` child + `PlayerCarry` component.
- `Assets/Scripts/Infrastructure/Services/Input/IInputService.cs` — add `InteractStarted`/`InteractCanceled` events.
- `Assets/Scripts/Infrastructure/Services/Input/InputService.cs` — reflection bind `Gameplay/Interact` action.
- `Assets/Scripts/Infrastructure/Providers/Configs/IConfigDataProvider.cs` + `ConfigDataProvider.cs` — add `Carry` getter + load CarryConfig.
- `Assets/Scripts/Data/Configs/WorldGenConfig.cs` — add `ComponentSpawnChance`.
- `Assets/Resources/Configs/World/WorldGenConfig.asset` — set new field.
- `Assets/Scripts/Infrastructure/Installers/GameSceneInstaller.cs` — `[SerializeField] GameObject _componentItemPrefab` + bind `IComponentSpawnService`.
- `Assets/Scripts/Infrastructure/Installers/ProjectInstaller.cs` — bind `IComponentPresetRegistry`.
- `ProjectSettings/TagManager.asset` — add layer `Carryable`.
- `Assets/Settings/Input/PlayerControls.inputactions` (user-edited in Unity) — add `Interact` action + `<Keyboard>/e` binding.
