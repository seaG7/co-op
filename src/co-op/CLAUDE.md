# CLAUDE.md

Co-op third-person multiplayer game (dark fantasy, 2 players, round-based, FishNet).
Game design: see [GDD.md](GDD.md). Architecture spec: `docs/superpowers/specs/2026-05-23-coop-bootstrap-design.md`.

## Stack
- Unity 6 (URP), Input System (new)
- **Zenject** — DI (ProjectContext / SceneContext / GameObjectContext)
- **UniTask** — all async (no coroutines, no `async void` except Unity event handlers)
- **FishNet** — networking (owner-authoritative for now)
- UI — MVP (View + Presenter, plain-C# presenter)
- Signals — local lightweight `SignalBus` (`Assets/Scripts/Signals/`), NOT Zenject's Signals extension

## Project structure (layer folders, not feature folders)
```
Assets/Scripts/
  Bootstrap/        EntryPoint (boot)
  Core/             StateMachine + States (Bootstrap→LoadMainMenu→MainMenu→LoadGame→Gameplay→GameOver)
  Data/             Configs (ScriptableObject types), Paths (const strings)
  Infrastructure/
    Installers/     ALL MonoInstallers (Project/GameScene/Player)
    Services/       ALL services (Network, UI, Scene, Input, Player, Camera, Spawn, Providers)
    Factories/      object + UI factories
  Gameplay/         ONLY MonoBehaviour/NetworkBehaviour + pure gameplay logic
    Player/         PlayerNetwork, Movement/, Look/, Camera/PlayerCameraRig, Animation/
    Spawn/          SpawnPoint (scene marker only)
  UI/               Common (WindowView<T>), per-screen View+Presenter
  Signals/          SignalBus + signal structs
  EditorTools/      editor-only menus/gizmos
```
**Rule:** any `*Installer` or `*Service` lives in `Infrastructure/`. Gameplay holds behaviours and math, never services/installers.

## Configs — single source of truth
- Config classes (ScriptableObject) live in `Data/Configs/`. Assets live in `Resources/Configs/<group>/`.
- `IConfigDataProvider` is the ONLY runtime accessor: `Windows`, `Network`, `Movement`, `GetWindowPrefab(id)`.
- NO installer holds `[SerializeField]` config refs. NO service takes a config in its constructor — inject `IConfigDataProvider`, read `provider.Network.X` on demand.
- `BootstrapState.EnterAsync` awaits `LoadAsync()` before any state that reads a config.
- Exception: per-player plain-C# (`MovementCalculator`/`JumpController`/`GroundProbe`) take `MovementConfig` directly (testable); bound via `FromMethod(ctx => ctx.Container.Resolve<IConfigDataProvider>().Movement)` in `PlayerInstaller`.

## Zenject — scopes & injection
- **ProjectContext** (`ProjectInstaller`, prefab in `Resources/`): whole-app services — config provider, scene loader, UI, input, network, session, player service, state machine, signals.
- **SceneContext** (`GameSceneInstaller`, on Game.unity): scene-bound — spawn registry/service (needs scene `SpawnPoint[]`), camera service, network event bridge.
- **GameObjectContext** (`PlayerInstaller`, on Player.prefab): per-player — movement math services. Inherits Scene→Project.
- A scope sees all parent bindings; never the reverse.
- **NetworkBehaviour injection:** Player.prefab has `GameObjectContext` + `PlayerInstaller`. `[Inject]` fields on the prefab's components are filled when GameObjectContext runs — works for both server (`InstantiatePrefab`) and client (FishNet auto-spawn, GameObjectContext.Awake finds SceneContext automatically). No manual injector needed.
- EntryPoint is NOT in a SceneContext — it self-injects via `ProjectContext.Instance.Container.Inject(this)`.

## FishNet patterns
- Authority: **owner-authoritative**. Owner reads input → moves CharacterController → `NetworkTransform` (Client Authoritative) replicates. Non-owners interpolate.
- NetworkBehaviour gates owner logic with `if (!base.IsOwner) return;` inside `OnStartClient`/`Update`.
- Player spawn: `PlayerSpawnService` (server-only logic) subscribes to `ServerManager.OnRemoteConnectionState` AND iterates `ServerManager.Clients` on Initialize (catches host's own client which connects before Game scene loads).
- Local player registration: `PlayerNetwork.OnStartClient` → if owner → `IPlayerService.RegisterLocalPlayer(this)`. Consumers `await IPlayerService.WaitForLocalPlayerAsync(ct)`.
- NetworkManager prefab: instantiate at scene ROOT (never under ProjectContext.transform — its `DontDestroyOnLoad(this)` no-ops on non-root in Unity 6 and breaks TimeManager).

## Code patterns
- **State machine:** `IState { EnterAsync(ct); ExitAsync(ct); }`. States chain via `await _stateMachine.EnterAsync<Next>(ct)`. Machine holds no shared CTS — each call is self-contained; states own their own CTS for mid-flight cancel.
- **UI MVP:** `WindowView<TPresenter>` instantiates its presenter via `Container.Instantiate<TPresenter>(new object[]{ this })`. Presenter is plain C#, takes the view first arg. `UIFactory` creates window prefabs under a plain `UIRoot` GameObject; each window prefab brings its OWN Canvas (root canvas, own sortingOrder).
- **Signals:** `SignalBus.Fire/Subscribe/Unsubscribe`. Local events only. Network events come via FishNet RPC/state, translated to signals by `NetworkEventBridge`.
- **Logic vs glue:** put logic in plain C# classes (testable). NetworkBehaviour/MonoBehaviour are thin glue that `[Inject]` plain-C# services and forward calls.

## Async / threading gotchas
- Use `UniTask.Delay(timeout, cancellationToken: ct)` for timeouts — runs on PlayerLoop (main thread). NEVER `CancellationTokenSource.CancelAfter` (System.Threading.Timer → ThreadPool → Unity API exceptions in continuations).
- `TimeoutWithoutException<T>` returns `(bool IsTimeout, T Result)` — `IsTimeout==true` means timed out (don't invert).
- All `await` in gameplay flows must stay on main thread; if a cancellation callback could fire off-thread, the whole continuation chain runs off-thread.

## Workflow notes
- Do NOT commit to git unless asked — user commits themselves.
- Config assets, prefabs, scenes, `.inputactions` are set up manually in Unity Editor (or via UnityMCP); code is written to expect them and degrade gracefully if missing (logs, no hard crash).
- `Tools/CoOp/*` editor menu: scene switching, config/prefab validation, playmode Host/Client auto-launch (for ParrelSync two-editor testing).
