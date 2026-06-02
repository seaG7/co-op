# Co-op Bootstrap Architecture — Design Spec

**Date:** 2026-05-23
**Project:** `D:/Reps/co-op/src/co-op`
**Status:** Draft (pre-implementation)

## 0. Goals & Non-goals

### Goals
- Clean, scalable root architecture for a Unity 3D multiplayer co-op game built on **Zenject + UniTask + FishNet**.
- Working flow `Bootstrap → MainMenu → Game` with MainMenu buttons `Host` (New Game) and `Connect`.
- Player spawn on Game scene at a chosen spawn point. Replicates correctly between server and clients via FishNet.
- Best-practice answer to the cross-cutting problems: local-player DI, local vs network events, init order, race conditions.
- UI MVP pattern (View + Presenter + minimal Model), scalable per screen.
- Basic input (`Move`/`Look` via Unity Input System), no concrete movement controller yet.

### Non-goals (explicit YAGNI)
- Save / load (no save system in root; co-op saves are server-side and will be designed when we know what to save).
- Lobby / matchmaking / session discovery.
- Linux Dedicated Server bootstrap branch (architecture stays compatible; the branch itself is deferred).
- Concrete `PlayerMovement`, `PlayerCameraController`, third-person/first-person camera logic.
- Steam / Relay / NAT traversal — LAN/direct-IP only.
- Rebinding, gamepad bindings, mobile controls.

## 1. Stack

| Concern | Choice |
|---|---|
| DI | Zenject (with `SignalBus`, `GameObjectContext`, `SceneContext`) |
| Async | UniTask |
| Networking | FishNet |
| Input | Unity Input System (`PlayerControls.inputactions` → generated C# class) |
| UI | MVP (View `MonoBehaviour` + plain-C# Presenter, single `WindowView<T>` base) |
| Render | URP 17.3 |

### Multiplayer scenarios
- **Dev:** Host + clients on LAN, or across networks via virtual LAN (e.g., ZeroTier). Tested in two Unity Editors via **ParrelSync** clone.
- **Prod (deferred):** Linux Dedicated Server build. Architecture stays compatible — server-authoritative state machine, scene flow goes through `INetworkService.LoadGlobalSceneAsync`.

## 2. Folder structure & assembly definitions

```
Assets/Scripts/
├── Bootstrap/                            CoOp.Bootstrap.asmdef
│   └── EntryPoint.cs                       # MonoBehaviour in Bootstrap.unity, kicks off state machine
│
├── Core/                                 CoOp.Core.asmdef
│   ├── StateMachine/{IState, IGameStateMachine, GameStateMachine}.cs
│   └── States/{Bootstrap, LoadMainMenu, MainMenu, LoadGame, Gameplay, GameOver}State.cs
│
├── Data/                                 CoOp.Data.asmdef
│   ├── Configs/{WindowsConfig, NetworkConfig}.cs
│   └── Paths/{ConfigPaths, ScenePaths}.cs
│
├── Infrastructure/                       CoOp.Infrastructure.asmdef
│   ├── Installers/{ProjectInstaller, GameSceneInstaller}.cs
│   ├── Factories/Objects/{IGameObjectFactory, GameObjectFactory}.cs
│   ├── Factories/UI/{IUIFactory, UIFactory}.cs
│   ├── Providers/Configs/{IConfigDataProvider, ConfigDataProvider}.cs
│   └── Services/
│       ├── Scene/{ISceneLoaderService, SceneLoaderService, ILoadingScreenService, LoadingScreenService}.cs
│       ├── UI/{IWindowService, WindowService, WindowID}.cs
│       ├── Input/{IInputService, InputService}.cs
│       ├── Network/{INetworkService, NetworkService, ISessionService, SessionService, NetworkEventBridge}.cs
│       └── Player/{IPlayerService, PlayerService}.cs
│
├── Gameplay/                             CoOp.Gameplay.asmdef
│   ├── Player/{PlayerInstaller, PlayerNetwork}.cs
│   └── Spawn/{SpawnPoint, ISpawnPointRegistry, SpawnPointRegistry, IPlayerSpawnService, PlayerSpawnService}.cs
│
├── UI/                                   CoOp.UI.asmdef
│   ├── Common/{IPresenter, WindowView, WindowView`1, EmptyPresenter, LoadingBarView}.cs
│   ├── MainMenu/{MainMenuView, MainMenuPresenter}.cs
│   ├── Connect/{ConnectView, ConnectPresenter, ConnectFormModel}.cs
│   ├── HUD/{HUDView, HUDPresenter}.cs
│   └── GameOver/{GameOverView, GameOverPresenter}.cs
│
├── Signals/                              (part of CoOp.Core.asmdef)
│   ├── NetworkSignals.cs                   # ServerStarted, ClientConnected/Disconnected, ConnectionFailed/Lost
│   ├── GameSignals.cs                      # LocalPlayerSpawned, GameStarted/Ended, SpawnFailed
│   └── UISignals.cs                        # reserved
│
└── EditorTools/                          CoOp.EditorTools.asmdef  (editor only)
    ├── CoOpToolsMenu.cs                    # Tools/CoOp/* menu items
    ├── SpawnPointSceneOverlay.cs           # scene-view lines between spawn points
    └── WindowRecordDrawer.cs               # compact inspector for WindowsConfig

Assets/Plugins/
├── UniTask/                                # already installed
└── FishNet/                                # to be installed via .unitypackage

Assets/Resources/
├── ProjectContext.prefab                   # exists; will be rebuilt with new ProjectInstaller
└── Configs/
    ├── UI/WindowsConfig.asset
    └── Network/NetworkConfig.asset

Assets/Prefabs/
├── Network/NetworkManager.prefab           # FishNet NetworkManager + Tugboat transport
├── Player/Player.prefab                    # NetworkObject + GameObjectContext + PlayerInstaller + PlayerNetwork
└── UI/{MainMenuWindow, ConnectWindow, LoadingScreen, HUD, GameOverScreen}.prefab

Assets/Scenes/
├── Bootstrap.unity                         # EntryPoint, EventSystem, idle camera
├── MainMenu.unity                          # EventSystem, UI camera (window is instantiated by UIFactory)
└── Game.unity                              # SceneContext, GameSceneInstaller, SpawnPoint[], camera placeholder

Assets/Settings/Input/
└── PlayerControls.inputactions             # Gameplay map (Move WASD, Look mouse delta) → auto-generated PlayerControls.cs

Assets/Tests/EditMode/                     CoOp.Tests.EditMode.asmdef
├── GameStateMachineTests.cs
└── PlayerServiceTests.cs
```

**Asmdef edges** (each line: `asmdef → asmdef`):
```
CoOp.Data            → (Unity modules)
CoOp.Core            → CoOp.Data
CoOp.Infrastructure  → CoOp.Data, CoOp.Core, Zenject, UniTask, FishNet
CoOp.Gameplay        → CoOp.Infrastructure, CoOp.Core, FishNet
CoOp.UI              → CoOp.Infrastructure, CoOp.Core
CoOp.Bootstrap       → CoOp.Infrastructure, CoOp.Core, CoOp.Gameplay, CoOp.UI
CoOp.EditorTools     → all above (editor-only)
CoOp.Tests.EditMode  → CoOp.Core, CoOp.Infrastructure, UniTask, NSubstitute
```

## 3. DI scopes

```
ProjectContext  (whole-game)
  └─ inherits → SceneContext (Game.unity, lifetime of scene)
        └─ inherits → GameObjectContext (Player.prefab, lifetime of instance)
```

### Configs — single source of truth
All `ScriptableObject` config classes live under `Assets/Scripts/Data/Configs/`; all `.asset` instances live under `Assets/Resources/Configs/<group>/`. `IConfigDataProvider` is the only way to obtain a config at runtime:

```csharp
public interface IConfigDataProvider
{
    UniTask LoadAsync(CancellationToken ct = default);
    WindowsConfig Windows { get; }
    NetworkConfig Network { get; }
    MovementConfig Movement { get; }
    GameObject GetWindowPrefab(WindowID id);
}
```

**Hard rules:**
- No installer holds `[SerializeField]` references to config assets. Configs are never "wired through the inspector".
- Services inject `IConfigDataProvider` and read the typed property on demand (e.g. `_configs.Network.DefaultPort`). By the time any non-bootstrap code touches a config, `BootstrapState` has awaited `LoadAsync`.
- Single exception: per-player plain-C# services (`MovementCalculator`, `JumpController`, `GroundProbe`) take `MovementConfig` in their constructor to stay unit-testable. The binding in `PlayerInstaller` is `FromMethod(ctx => ctx.Container.Resolve<IConfigDataProvider>().Movement)` — config still flows from the provider; Zenject does the indirection.
- `LoadAsync` loads all configs in parallel via `UniTask.WhenAll`.

### ProjectContext (ProjectInstaller) — global services
- `IConfigDataProvider` (the one place that knows about config assets; everything else asks it)
- `ISceneLoaderService`, `ILoadingScreenService` (the latter goes through `WindowService`)
- `IUIFactory`, `IWindowService`
- `IGameObjectFactory`
- `IInputService` (created here; `Gameplay` action map enabled only during `GameplayState`)
- `NetworkManager` (FishNet, instantiated via `FromComponentInNewPrefab` at scene root; FishNet's own `_dontDestroyOnLoad=true` field handles persistence. **Do not parent under ProjectContext.transform** — `DontDestroyOnLoad` no-ops on non-root in Unity 6 and breaks TimeManager state-event dispatch.)
- `INetworkService`, `ISessionService` (both inject `IConfigDataProvider`, no direct `NetworkConfig`)
- `IPlayerService` (tracks `LocalPlayer`; project-lifetime concept — `null` in MainMenu is valid)
- `IGameStateMachine`, all states (`AsTransient`) — states inject `IConfigDataProvider` for port/address lookup
- `SignalBus` (`SignalBusInstaller.Install`) + all signal declarations
- UI presenters (`AsTransient`)

### SceneContext (Game.unity, GameSceneInstaller) — scene-bound services
Bound here only when a service either (a) needs scene-object references via SerializeField, or (b) uses the scene-scope `DiContainer` to instantiate prefabs into the scene.
- `ISpawnPointRegistry` (constructed from `SpawnPoint[]` on the installer)
- `IPlayerSpawnService` (uses scene container for `InstantiatePrefab`; server-only logic gated internally)
- `NetworkEventBridge` (NonLazy; subscribes to FishNet `SceneManager.OnLoadEnd`)

**Why `IPlayerService` is project-scope, not scene-scope:** "Where is the local player?" is a project-wide question — the answer is `null` in MainMenu, populated in Game. State resets naturally because `PlayerNetwork.OnStopClient` clears `LocalPlayer` when the `NetworkObject` despawns. The waiters list is gated per-state CTS, not by scope. Putting `IPlayerService` in scene scope would force its consumers (`GameplayState`, `HUDPresenter`, `DebugOverlay`) into scene scope too, which is a worse fit for code that doesn't otherwise depend on scene objects.

### GameObjectContext (Player.prefab, PlayerInstaller) — per-player services
- `PlayerNetwork` (`FromComponentOnRoot`)
- Reserved spots for future `IPlayerMovement`, `IPlayerCameraController`, `IPlayerInputBinding`

### Initialization order

`IInitializable` is used **only for lightweight synchronous wiring** (subscribe to events, build in-memory objects). Anything that does I/O (load resources, scenes, network connect) lives in `BootstrapState.EnterAsync` under explicit `await`. This rule keeps the cost model of `Initialize()` visible — no surprise blocking, no hidden async-over-sync.

| Service | IInitializable? | Execution order | Why |
|---|---|---|---|
| `ConfigDataProvider` | ✗ | — | Async-only via `LoadAsync(ct)`, explicitly awaited in `BootstrapState` |
| `InputService` | ✓ | default | Creates `PlayerControls`; independent of other services |
| `NetworkService` | ✓ | `-40` | Subscribes to FishNet `NetworkManager` events; before SessionService |
| `SessionService` | ✓ | `-30` | Subscribes to NetworkService C# events; documented after-NetworkService |
| `LoadingScreenService`, `WindowService`, `GameStateMachine` | ✗ | — | Stateless until first call |

Explicit `BindExecutionOrder` is kept only where ordering is load-bearing (the NetworkService → SessionService chain). Other services use default Zenject ordering.

## 4. State machine

```csharp
public interface IState
{
    UniTask EnterAsync(CancellationToken ct);
    UniTask ExitAsync(CancellationToken ct);
}

public interface IGameStateMachine
{
    IState CurrentState { get; }
    UniTask EnterAsync<TState>(CancellationToken ct = default) where TState : class, IState;
}
```

States return after setup; they "stay active" because `CurrentState` references them. Transitions happen from outside (button click, signal handler).

### States and what each does
| State | EnterAsync does | ExitAsync does |
|---|---|---|
| `BootstrapState` | `await ConfigDataProvider.LoadAsync`; `await stateMachine.EnterAsync<LoadMainMenuState>` | — |
| `LoadMainMenuState` | `loading.Show`; `await sceneLoader.LoadSceneAsync(MAIN_MENU)`; `loading.Hide`; `await stateMachine.EnterAsync<MainMenuState>` | — |
| `MainMenuState` | `windowService.Open(MainMenu)` | `windowService.Close(MainMenu)` |
| `LoadGameState` | close menu; `loading.Show`; **branch by `session.State`** — Disconnected → `StartHostAsync` then `LoadGlobalSceneAsync(GAME)`; Connected → `WaitForSceneLoadedAsync(GAME)`; on fail rollback to MainMenu; `loading.Hide`; `await stateMachine.EnterAsync<GameplayState>` | — |
| `GameplayState` | subscribe `ConnectionLost/Failed/SpawnFailed` signals; `windowService.Open(HUD)`; `inputService.Enable()`; `await playerService.WaitForLocalPlayerAsync(ct)` | unsubscribe; `inputService.Disable()`; `windowService.Close(HUD)`; cancel CTS |
| `GameOverState` | `windowService.Open(GameOver)` | `windowService.Close(GameOver)` |

### Cancellation & exceptions in GameStateMachine
The machine itself holds **no mutable cancellation state**. Each `EnterAsync<TState>(ct)` call is self-contained: it uses only the supplied `ct`. This lets nested transitions (a state's `EnterAsync` chaining the next state) work naturally — there is no machine-level `_cts` to clobber.

- Flow: `EnterAsync<TState>(ct)` → if same state, no-op → `await previous.ExitAsync(ct)` (catches OCE/other) → resolve `TState` → `CurrentState = next` → `await next.EnterAsync(ct)` (OCE swallowed as expected, other exceptions fire `OnEnterFailed` for caller-defined recovery).
- **Per-state mid-flight cancellation** (e.g. `GameplayState` aborting its `WaitForLocalPlayer` when `ConnectionLostSignal` fires) is the state's responsibility: the state creates its own `CancellationTokenSource` inside `EnterAsync` and cancels it in `ExitAsync` or from a signal handler. The state machine triggers that path by calling `ExitAsync` on the outgoing state before entering the new one.
- `OnEnterFailed` is wired by `EntryPoint` to fall back to `LoadMainMenuState`, with the handler responsible for its own recursion guard (skip if already in `LoadMainMenu` / `MainMenu` / `Bootstrap`).
- Idempotent: re-entering current state type is a no-op.

## 5. Network layer

### `INetworkService` (FishNet wrapper)
```csharp
public interface INetworkService
{
    bool IsServer { get; }
    bool IsClient { get; }
    bool IsHost => IsServer && IsClient;
    NetworkManager NetworkManager { get; }    // exposed for SpawnService and Bridge only

    UniTask<bool> StartServerAsync(ushort port, CancellationToken ct = default);
    UniTask<bool> StartClientAsync(string address, ushort port, CancellationToken ct = default);
    UniTask StopAsync(CancellationToken ct = default);

    UniTask LoadGlobalSceneAsync(string sceneName, CancellationToken ct = default);
    UniTask WaitForSceneLoadedAsync(string sceneName, CancellationToken ct = default);

    event Action ServerStarted, ServerStopped, ClientStarted, ClientStopped;
    event Action<string> ConnectionFailed;
}
```

- `StartServerAsync` / `StartClientAsync` use `UniTaskCompletionSource` + `TimeoutWithoutException` (timeout from `NetworkConfig.ConnectTimeoutSec`).
- `LoadGlobalSceneAsync` wraps `NetworkManager.SceneManager.LoadGlobalScenes` (server-side; replicates to clients automatically). Uses `SceneLoadData(...) { ReplaceScenes = ReplaceOption.All }` to replace MainMenu rather than additive.
- `WaitForSceneLoadedAsync` subscribes to `SceneManager.OnLoadEnd` filtered by name (client side after host pushes scene).

### `ISessionService` (high-level facade)
```csharp
public enum SessionState { Disconnected, StartingServer, StartingClient, Connected, Disconnecting, Failed }

public interface ISessionService
{
    SessionState State { get; }
    string LastError { get; }
    int LocalClientId { get; }
    IReadOnlyList<int> ConnectedClientIds { get; }

    event Action<SessionState> StateChanged;
    event Action<int> ClientJoined, ClientLeft;

    UniTask<bool> StartHostAsync(ushort port, CancellationToken ct = default);   // server + localhost client
    UniTask<bool> JoinAsync(string address, ushort port, CancellationToken ct = default);
    UniTask LeaveAsync(CancellationToken ct = default);
}
```

- Tracks `ConnectedClientIds` via `ServerManager.OnRemoteConnectionState`.
- Distinguishes intentional `LeaveAsync` from `ConnectionLost` by checking `_state == Disconnecting` in `OnClientStopped`.
- Fires `SignalBus` signals: `ServerStartedSignal`, `ClientConnectedSignal`, `ClientDisconnectedSignal`, `ConnectionFailedSignal`, `ConnectionLostSignal`.

### `NetworkEventBridge` (scene-scope, NonLazy)
- Translates FishNet network events into local `SignalBus` signals so UI/gameplay only listen to one source.
- Root scope: fires `GameStartedSignal` on `SceneManager.OnLoadEnd` for Game scene. More subscriptions added when game broadcasts appear.

### `NetworkConfig` (`Resources/Configs/Network/NetworkConfig.asset`)
```
DefaultPort         = 7777
DefaultAddress      = "127.0.0.1"
LocalhostAddress    = "127.0.0.1"
ConnectTimeoutSec   = 10
```

## 6. UI MVP layer

### Base types
```csharp
public interface IPresenter : IDisposable { void Initialize(); }

public abstract class WindowView : MonoBehaviour
{
    public abstract void BindPresenter();
    public abstract void UnbindPresenter();
}

public abstract class WindowView<TPresenter> : WindowView where TPresenter : class, IPresenter
{
    [Inject] private DiContainer _container;
    protected TPresenter Presenter { get; private set; }

    public sealed override void BindPresenter()
    {
        Presenter = _container.Instantiate<TPresenter>(new object[] { this });
        Presenter.Initialize();
        OnBound();
    }

    public sealed override void UnbindPresenter()
    {
        OnUnbinding();
        Presenter?.Dispose();
        Presenter = null;
    }

    protected virtual void OnBound() { }
    protected virtual void OnUnbinding() { }
}
```

Each concrete `*View` derives from `WindowView<*Presenter>`. View owns its presenter type genericially — no reflection, no attributes.

### `IUIFactory` / `UIFactory`
- `CreateScreen(WindowID)`: looks up prefab from `IConfigDataProvider` → `_container.InstantiatePrefab` under a persistent `UIRoot` → `view.BindPresenter()` → store.
- `DestroyScreen(WindowID)`: `view.UnbindPresenter()` (calls `Dispose` on presenter) → `Object.Destroy(go)`.
- Idempotent: repeated `CreateScreen` returns existing instance.
- `UIRoot` is a `DontDestroyOnLoad` GameObject so windows survive scene swaps (important for `LoadingScreen` during `MainMenu → Game`).

### `IWindowService` (refactored)
Removed dependency on the now-deleted `IGameStateService`. Pure facade:
```csharp
public interface IWindowService
{
    bool IsWindowOpened(WindowID id);
    WindowView Open(WindowID id);
    T OpenAndGet<T>(WindowID id) where T : WindowView;
    T Get<T>(WindowID id) where T : Component;
    void Close(WindowID id);
}
```

### Concrete screens
- **MainMenu** — buttons `Host` (→ `LoadGameState`), `Connect` (→ open `ConnectWindow`), `Quit`. Presenter dims buttons when `session.State != Disconnected/Failed`.
- **Connect** — IP / Port inputs, Connect/Back buttons, error label, busy indicator. Presenter has `ConnectFormModel`, calls `session.JoinAsync`, renders busy/error. On success: closes self, transitions to `LoadGameState`.
- **HUD** — debug status text (state, ClientId, has-local-player) for root; will grow.
- **GameOver** — back-to-menu button → `session.LeaveAsync` + transition.
- **LoadingScreen** — `LoadingBarView` with progress bar, presenter is `EmptyPresenter`.

### Window opening responsibility
| Window | Opener | Closer |
|---|---|---|
| MainMenu | `MainMenuState.EnterAsync` | `LoadGameState.EnterAsync` |
| Connect | `MainMenuPresenter.OnConnect` | `ConnectPresenter.OnBack` / on join success |
| Loading | `LoadingScreenService.Show` | `LoadingScreenService.Hide` |
| HUD | `GameplayState.EnterAsync` | `GameplayState.ExitAsync` |
| GameOver | `GameOverState.EnterAsync` | `GameOverPresenter.OnBack` |

### `WindowsConfig`
Fix: `WindowRecord` must be `[Serializable] class` (current code has `struct` without attribute; won't render in inspector). Optional `WindowRecordDrawer` makes inspector compact.

## 7. Input layer

### `.inputactions`
- File: `Assets/Settings/Input/PlayerControls.inputactions`, **Generate C# Class = yes** → `PlayerControls.cs` in `namespace CoOp.Input`.
- One Action Map `Gameplay`:
  - `Move` — `Value/Vector2`, 2D Vector composite (W/S/A/D)
  - `Look` — `Value/Vector2`, Mouse Delta
- UI events stay on `InputSystemUIInputModule` (EventSystem), not in `IInputService`.

### `IInputService`
```csharp
public interface IInputService : IDisposable
{
    Vector2 MoveAxis { get; }
    Vector2 LookAxis { get; }
    bool IsEnabled { get; }
    event Action<Vector2> MoveChanged, LookChanged;
    void Enable();
    void Disable();
}
```
- Both poll-based getters (for Update loops) and event-based (for rare reactions).
- `IInitializable` constructs `PlayerControls`. **Does NOT enable** — `GameplayState` enables, `GameplayState.ExitAsync` disables.
- Single instance project-scope. Per-player consumers (`PlayerMovement`, future) gate on `base.IsOwner`.

## 8. Player spawn

### `SpawnPoint` (MonoBehaviour marker)
- Reads `transform.position` / `transform.rotation`.
- `OnDrawGizmos` — semi-transparent sphere + forward arrow + random color (set in `Reset`).
- `OnDrawGizmosSelected` — `Handles.Label(name)`.
- Editor-only `SpawnPointSceneOverlay` draws thin lines between all spawn points in scene view.

### `ISpawnPointRegistry`
- Constructed with `SpawnPoint[]` from `GameSceneInstaller` SerializeField.
- `GetForConnection(NetworkConnection conn)` → `points[abs(conn.ClientId) % N]`. Stable per session; falls back to origin if empty (with error log).

### `IPlayerSpawnService` (scene-scope, server-only logic)
- `IInitializable.Initialize`: subscribes to `ServerManager.OnRemoteConnectionState` (server-only logic gated by `_network.IsServer`).
- `SpawnPlayerAsync(NetworkConnection)`: `_sceneContainer.InstantiatePrefab(playerPrefab, pos, rot, null)` → FishNet `ServerManager.Spawn(go, conn)` → `await UniTask.Yield`.
- On `RemoteConnectionState.Stopped`: `DespawnPlayer` (calls `ServerManager.Despawn`).
- Maintains `Dictionary<int, PlayerNetwork>` keyed by ClientId; cleans up in `Dispose`.
- Fires `SpawnFailedSignal(clientId, reason)` on failures.

### `PlayerNetwork` (NetworkBehaviour on Player.prefab)
- `[Inject] IPlayerService _playerService;`  `[Inject] SignalBus _signalBus;`
- `OnStartClient`: if `IsOwner`, `_playerService.RegisterLocalPlayer(this)` and fire `LocalPlayerSpawnedSignal`.
- `OnStopClient`: if `IsOwner`, `_playerService.UnregisterLocalPlayer(this)`.
- **No `Awake` overrides for DI**: `GameObjectContext.Awake` automatically finds `SceneContext` in the same scene → no `NetworkContextRunner` needed.

### `IPlayerService` (scene-scope)
- Stores `LocalPlayer` reference, fires `LocalPlayerAssigned/Removed`.
- `WaitForLocalPlayerAsync(ct)`: returns immediately if already assigned; otherwise registers a `UniTaskCompletionSource` that completes on `RegisterLocalPlayer`. Cancellable via `ct`. `UnregisterLocalPlayer` cancels pending waiters.

### Player.prefab composition
```
Player (root)
├─ Transform
├─ CharacterController              (height ≈ 2, radius ≈ 0.4, center ≈ (0,1,0))
├─ NetworkObject                    (FishNet; registered in DefaultPrefabObjects)
├─ NetworkTransform                 (FishNet; "Client Authoritative" mode, position+rotation sync)
├─ GameObjectContext                (Zenject)
├─ PlayerInstaller                  (assign MovementConfig + ground LayerMask)
├─ PlayerNetwork                    (NetworkBehaviour, LocalPlayer registration)
├─ PlayerMovement                   (NetworkBehaviour, owner drives CC; non-owner derives Snapshot from transform deltas)
├─ PlayerLookController             (NetworkBehaviour, owner yaw→player root, pitch local)
├─ PlayerCameraRig                  (NetworkBehaviour, owner-only LateUpdate drives Camera follow)
├─ PlayerAnimationDriver            (MonoBehaviour, no-op until Animator assigned — wired to Snapshot for blend-tree params)
├─ CameraPivot (child Transform)    — at head height (~1.6 m); pitch applied here, camera trails behind
└─ Visual (child)                   — placeholder Capsule mesh; future humanoid + Animator
```

### Movement architecture (plain C# + thin NetworkBehaviour glue)

All logic lives in plain C# services; the NetworkBehaviours hold only injected references and call into them.

| Class | Kind | Responsibility |
|---|---|---|
| `MovementConfig` | `ScriptableObject` | All tunables (speed, accel, gravity, jump height, coyote/buffer windows, slope, air control). Single source of truth. |
| `MovementSnapshot` | `readonly struct` | Per-frame state: local velocity, horizontal speed, IsGrounded, WasJustGrounded/Airborne, JumpJustExecuted, vertical velocity, slope angle. Consumed by animation driver. |
| `MovementCalculator` | plain C# | Horizontal velocity solver. Accel + decel with air-control coefficient; input→world projection. |
| `JumpController` | plain C# | Jump state machine: variable height (release-to-cut), coyote time, jump buffer, apex gravity multiplier, fall gravity multiplier, max fall speed. |
| `GroundProbe` | plain C# | `SphereCast` ground check with slope angle. More robust than `CharacterController.isGrounded`. |
| `PlayerMovement` | `NetworkBehaviour` | Owner: read input → calc → `CC.Move`. Non-owner: derive snapshot from `transform.position` delta (driven by `NetworkTransform`). |
| `PlayerLookController` | `NetworkBehaviour` | Owner reads `IInputService.LookAxis`. Yaw → `transform.Rotate` (replicates via `NetworkTransform`). Pitch → local float consumed by camera rig. |
| `PlayerCameraRig` | `NetworkBehaviour` | Owner-only `LateUpdate`: rotates `CameraPivot` by pitch, smoothly follows pivot + offset-behind with `Camera.LookAt(pivot)`. |
| `PlayerAnimationDriver` | `MonoBehaviour` | Pushes `MovementSnapshot` into `Animator` params (Speed, LocalVelX/Z, IsGrounded, VerticalVelocity, JumpTrigger, LandTrigger). No-op without Animator. Runs on all clients. |
| `ICameraService` / `CameraService` | plain C# (scene-scope) | Resolves the game `Camera`. Falls back to `Camera.main`. |

**Authority model:** owner-authoritative. The owner client moves itself (input → CC.Move), and `NetworkTransform` replicates position/rotation. Non-owners observe via transform interpolation. Trade-off: no server validation (cheatable). Acceptable for friends-only co-op; migration path to server-auth+prediction is contained to `PlayerMovement`.

**AAA-feel ingredients implemented:**
- Tight horizontal feel: separate accel/decel rates, both high
- Air control coefficient (~0.55) — partial steering mid-jump
- Variable jump height (release before apex → vertical velocity cut)
- Apex hang time (lower gravity near peak)
- Heavier fall gravity (snappier landing)
- Coyote time (~120ms): jump still works just after walking off ledge
- Jump buffer (~120ms): early Jump press triggers on landing
- Max fall speed cap

**Animation forward-compatibility:** `PlayerAnimationDriver` exposes the exact param names a blend tree expects. When you drop in an `Animator` with a Controller that defines those params, animations start running with no code change.

## 9. Error handling & recovery

### Failure modes and recovery
| Failure | Where caught | Recovery |
|---|---|---|
| Connect timeout / refused | `ConnectPresenter.OnConnect` reads `session.LastError` | Stay on ConnectWindow, show error |
| Host start failure | `LoadGameState.EnterAsync` | Rollback session, return to `LoadMainMenuState` |
| Mid-game disconnect | `ConnectionLostSignal` → `GameplayState` handler | `LoadMainMenuState` (clients silently return) |
| Scene load failure | `LoadGameState` try/catch | `session.LeaveAsync` + `LoadMainMenuState` |
| Player spawn failure | `SpawnFailedSignal` → `GameplayState` handler (only if my ClientId) | `LoadMainMenuState` |
| DI resolution | Editor: surface stack; build: log + stay on Bootstrap | Manual fix |
| State `EnterAsync` exception | `GameStateMachine` try/catch | Fallback to `LoadMainMenuState` (recursion-guarded) |

### Cancellation propagation
- Each `IState.EnterAsync(ct)` respects its CT. State machine cancels previous CT on transition; in-flight `await`s throw `OperationCanceledException`, which is caught as normal flow.

### UI race protections
- Double-click Host: `MainMenuPresenter` disables buttons when `session.State` ≠ Disconnected/Failed.
- Double-click Connect: `ConnectPresenter._model.Busy = true` during `JoinAsync`.
- Back during connecting: cancels presenter CTS → `JoinAsync` propagates cancellation → `session` resets.

### Logging convention
- All Debug calls prefixed `[ClassName]`: `Debug.Log("[SessionService] ...")`.
- `OnValidate` checks on installers — missing SerializeField references log via `Debug.LogError(msg, this)` so click-to-object works.

## 10. Developer tools

### Gizmos
- `SpawnPoint.OnDrawGizmos` — colored sphere + forward arrow.
- `SpawnPoint.OnDrawGizmosSelected` — text label.
- `SpawnPointSceneOverlay` — connect-the-dots lines between all spawn points in scene view (editor only).

### Editor menu (`Tools/CoOp/*`)
- `Scenes/Open Bootstrap | MainMenu | Game` — fast scene switching.
- `Validate/All` — runs all validators.
- `Validate/Windows Config` — verifies all `WindowID` values have a prefab; prefabs have `WindowView` component; no duplicates.
- `Validate/Player Prefab` — checks presence of `NetworkObject`, `GameObjectContext`, `PlayerInstaller`, `PlayerNetwork`.
- `Validate/SpawnPoints in Active Scene` — counts and warns if zero.
- `Validate/Network Prefab Registry` — checks `DefaultPrefabObjects` SO contains `Player.prefab`.
- `Playmode/Start Host (localhost)` and `Playmode/Start Client (localhost)` — set `EditorPrefs["CoOp.LaunchMode"]`, then enter play mode. `EntryPoint` reads this and skips MainMenu accordingly.

### OnValidate
- `ProjectInstaller`: `_networkManagerPrefab` not null. (No config SerializeFields — those are loaded by `IConfigDataProvider`.)
- `GameSceneInstaller`: `_playerPrefab` not null; `_spawnPoints` not empty (warning).

### ParrelSync
- Recommended: install via Package Manager git URL `https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync`.
- Workflow: clone via ParrelSync window → open clone editor → in original `Tools/CoOp/Playmode/Start Host`, in clone `Start Client` → two-editor multiplayer testing in seconds.

### Cross-network testing
- Documented in README only: ZeroTier (free, virtual LAN) for cross-network dev tests with a teammate.

## 11. Testing strategy

### EditMode unit tests
- `GameStateMachineTests` — transition correctness, ignore re-entry, cancellation, exception fallback.
- `PlayerServiceTests` — `WaitForLocalPlayerAsync` resolves on register; cancels on unregister; respects ct.
- `SpawnPointRegistryTests` — deterministic mapping by ClientId; empty registry behavior.
- `SessionServiceTests` — state transitions; intentional Leave vs ConnectionLost distinction.

### Out of scope (need PlayMode + FishNet stub)
- `NetworkService`, `PlayerSpawnService`, `UIFactory` — covered manually for now; add PlayMode tests when payoff justifies setup cost.

### Manual QA checklist (per change)
```
[ ] Bootstrap → Play → MainMenu (no console errors)
[ ] Host → Game scene, capsule on SpawnPoint_01, HUD opens
[ ] ParrelSync clone → Connect 127.0.0.1:7777 → both editors show 2 capsules
[ ] Close clone → host sees ClientDisconnected log + despawn
[ ] Connect to closed port → "Connection timeout" after 10s
[ ] Empty IP → instant "Invalid address"
[ ] ESC during connecting → cancels, can retry
```

## 12. Signals (declared in ProjectInstaller)

```csharp
// readonly structs (zero alloc)
ServerStartedSignal(ushort port)
ServerStoppedSignal
ClientConnectedSignal(int clientId)
ClientDisconnectedSignal(int clientId)
ConnectionFailedSignal(string reason)
ConnectionLostSignal(string reason)
LocalPlayerSpawnedSignal(PlayerNetwork player)
SpawnFailedSignal(int clientId, string reason)
GameStartedSignal
GameEndedSignal
```

All declared via `Container.DeclareSignal<T>()` in `ProjectInstaller.BindSignals()`. `SignalBus` from `SignalBusInstaller.Install(Container)`.

## 13. Implementation order

1. **Foundation (sec 1-3):** folder layout, asmdefs, `ProjectInstaller` skeleton, `GameSceneInstaller` skeleton, `PlayerInstaller` skeleton, `NetworkConfig` SO, `WindowsConfig` SO refactor (`WindowRecord` Serializable), signals declared. *Code review checkpoint: DI scope correctness, init order, asmdef edges.*
2. **Network (sec 4):** FishNet install, `NetworkManager.prefab`, `NetworkService`, `SessionService`, `NetworkEventBridge`. *Code review checkpoint: race conditions on connect/disconnect, lifecycle, event subscription cleanup.*
3. **UI (sec 5):** `WindowView` base, `UIFactory`, refactor `WindowService`, all View+Presenter pairs, `WindowsConfig` populated.
4. **Input (sec 6):** `.inputactions` asset, `InputService`.
5. **Spawn (sec 7):** `SpawnPoint`, `SpawnPointRegistry`, `PlayerSpawnService`, `PlayerService`, `Player.prefab`, spawn points placed in `Game.unity`. (unity-mcp for prefab/scene work.)
6. **States + recovery (sec 8):** All `IState` classes, `EntryPoint`, `GameStateMachine`, signal wiring, recovery flows. *Code review checkpoint: cancellation correctness, state transition graph, no leaked subscriptions.*
7. **Tools + tests (sec 9, 11):** Editor menu, OnValidate, ParrelSync note in README, 2-3 EditMode tests.
8. **Final review:** Run code-reviewer across full diff with focus on race conditions, DI, init order, over-engineering vs missing-architecture.

## 14. Explicit non-architecture decisions for future

- **Per-player input scope:** if split-screen ever needed, introduce `IPlayerInputBinding` in `PlayerInstaller`; current global `IInputService` becomes one-of-many readers.
- **Spawn point assignment:** current `ClientId % N` will be replaced by lobby slot reservation when lobby exists.
- **Session continuity / persistent identity:** when added, drop the assumption that ClientId is stable across reconnects.
- **DS bootstrap branch:** add `#if UNITY_SERVER` path in `BootstrapState` that calls `StartServerAsync` + scene load, skipping MainMenu entirely.
- **Save system:** server-authoritative state; design separately when there is something concrete to save.
