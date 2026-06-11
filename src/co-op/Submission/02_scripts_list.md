# «Хранители неба» — список собственных скриптов

Все скрипты — в `Assets/Scripts/` (ассембли `CoOp.Main`, `CoOp.Data`, `CoOp.EditorTools`). Плагины (FishNet, Zenject, UniTask, DOTween и пр.) и `DownloadedAssets` сюда не входят.

---

## 1. Сетевые объекты (FishNet `NetworkBehaviour`) — с указанием сетевых компонентов

| Скрипт | Назначение | Сетевые компоненты |
|---|---|---|
| `Gameplay/Net/InjectableNetworkBehaviour` | Базовый класс всех сетевых объектов: DI-инъекция в `OnStartNetwork` через scene-контейнер (идемпотентно, безопасно для пулинга) | `NetworkBehaviour`; `OnStartNetwork` |
| `Gameplay/Player/PlayerNetwork` | Корень игрока; регистрирует локального игрока (`ILocalPlayer`) | `NetworkBehaviour`; `OnStartClient` (owner) |
| `Gameplay/Player/Movement/PlayerMovement` | Движение игрока (owner-authoritative, `CharacterController`) | `NetworkBehaviour`; `NetworkTransform` (клиент-авторитетный) |
| `Gameplay/Player/Look/PlayerLookController` | Поворот тела и питч камеры; синхронизация питча для не-владельцев | `NetworkBehaviour`; `SyncVar<float>` (питч); `[ServerRpc]` `SubmitPitch` |
| `Gameplay/Player/Camera/PlayerCameraRig` | Камера от первого лица, тряска, эффекты (только у владельца) | `NetworkBehaviour` (owner-gated) |
| `Gameplay/Player/Carry/PlayerCarry` | Перенос предметов: серверный захват + клиентский «пин» | `NetworkBehaviour`; `[ServerRpc]` + `[ObserversRpc]` (grab/release/fx) |
| `Gameplay/Player/Combat/PlayerMelee` | Ближний бой (ЛКМ) по видимой позиции врага | `NetworkBehaviour`; `[ServerRpc]` (нанесение урона) |
| `Gameplay/Player/Combat/PlayerDrink` | Механика «выпить» (зажать E) | `NetworkBehaviour`; `[ServerRpc]` |
| `Gameplay/Player/Vitals/PlayerDrunk` | Состояние опьянения (стэк интенсивности) | `NetworkBehaviour`; `SyncVar` |
| `Gameplay/Player/Vitals/PlayerVitals` | Здоровье/нокдаун/спасение игрока; итог «все выбыли» | `NetworkBehaviour`; `SyncVar` (состояние); серверные `ServerKnockDown`/`ServerRevive` |
| `Gameplay/Player/Weapons/PlayerWeaponControl` | Управление пушкой, когда игрок «сел» за неё | `NetworkBehaviour` |
| `Gameplay/Player/View/PlayerModelVisibility` | Скрытие собственной модели от первого лица | `NetworkBehaviour` |
| `Gameplay/World/Enemies/Enemy` | Враг-паук: серверный FSM, цепляние за игрока/модуль, эффекты | `NetworkBehaviour`; `SyncVar` (id вцепившегося игрока); `[ObserversRpc]` (эффекты) |
| `Gameplay/World/Weapon/Weapon` | Пушка/гарпун: заряд трупами, выстрел, урон Источнику | `NetworkBehaviour`; `SyncVar` `CorpsesLoaded`; `[ServerRpc]`/`[ObserversRpc]` (выстрел) |
| `Gameplay/World/Weapon/WeaponModuleSlot` | Слот модуля: занятость, «бюджет хвата», эжект под атакой | `NetworkBehaviour`; `SyncVar` `IsOccupied`/`MobCount` |
| `Gameplay/World/Source/Source` | Вражеский Источник: фазы, спавн волн, уязвимость | `NetworkBehaviour`; `SyncVar` `State`; `[ObserversRpc]` |
| `Gameplay/World/Round/RoundNetworkController` | Итог раунда (победа/поражение) → сигнал клиентам | `NetworkBehaviour`; `SyncVar` `RoundOutcome`; `[ObserversRpc]` |
| `Gameplay/World/Items/Carryable` | Переносимый предмет: состояние захвата/установки в слот | `NetworkBehaviour`; `SyncVar` (`IsCarried`/`IsSnapped`) |
| `Gameplay/World/Items/Drinkable` | Переносимая «выпивка» | `NetworkBehaviour`; `SyncVar` |
| `Gameplay/World/Items/Corpse` | Маркер «труп» — заряд для пушки (на объекте `Carryable`) | — (логика на `Carryable`) |
| `Gameplay/World/Portals/Portal` | Портал (вырезается из дизайна) | `NetworkBehaviour` |
| `Gameplay/World/Weapon/CorpseIntake` | Триггер-зона загрузки трупа в пушку (серверная логика) | серверный триггер → `Weapon.AddCorpse` |

## 2. Сетевые сервисы и инфраструктура сети

| Скрипт | Назначение | Сетевые компоненты |
|---|---|---|
| `Infrastructure/Services/Network/NetworkService` | Обёртка над FishNet `NetworkManager`: старт сервера/клиента, остановка, загрузка глобальной сцены | `NetworkManager`, `ServerManager`/`ClientManager`, `SceneManager` |
| `Infrastructure/Services/Network/SessionService` | Сессия: `StartServerOnlyAsync`/`StartHostAsync`/`JoinAsync`, состояние, события клиентов | `ServerManager.Clients`, `OnRemoteConnectionState` |
| `Infrastructure/Services/Network/NetworkEventBridge` | Преобразует сетевые события сцены в локальный `SignalBus` | подписка на FishNet-события |
| `Infrastructure/Services/Lobby/LobbyService` | Лобби/комната: участники, ники, лидер, запуск игры | **FishNet Broadcast** (без `NetworkObject`): client→server (`SetNickname`/`RequestStart`), server→client (`LobbyState`/`GameStarting`) |
| `Infrastructure/Services/Lobby/LobbyBroadcasts` | Структуры сообщений лобби | `struct : IBroadcast` |
| `Infrastructure/Services/Spawn/PlayerSpawnService` | Серверный спавн игрока на каждое подключение | server, `OnRemoteConnectionState` + `ServerManager.Clients` |
| `Infrastructure/Services/Spawn/NetworkSpawnService` | Спавн/деспавн сетевых объектов | `ServerManager.Spawn`/`Despawn` |
| `Infrastructure/Services/Spawn/MarkerBasedSpawnService` | Серверный спавн объектов уровня по маркерам, `LevelReadySignal` | server spawn |
| `Infrastructure/Services/Spawn/WeaponBaseSpawner` | Спавн базы пушки на старте уровня | server spawn |
| `Infrastructure/Services/DI/SceneDiContainerRegistry` | Реестр scene-контейнера — через него сетевые объекты получают зависимости | используется в `OnStartNetwork` |

## 3. Состояния и ядро (плейн-C#)
- `Bootstrap/EntryPoint` — точка входа, само-инъекция и запуск конечного автомата.
- `Core/StateMachine/` — `IState`, `IGameStateMachine`, `GameStateMachine` (асинхронный КА на UniTask).
- `Core/States/` — `BootstrapState` (загрузка конфигов; на dedicated-сервере сразу поднимает сервер), `LoadMainMenuState`, `MainMenuState`, `LobbyState` (комната / ожидание на сервере), `LoadGameState` (загрузка сцены + прогресс-бар), `GameplayState`, `GameOverState`.

## 4. Инфраструктура (прочие сервисы)
- **UI:** `WindowService`, `UIFactory` (создание окон по `WindowID`), `LoadingScreenService` (экран загрузки с прогрессом).
- **Ввод:** `InputService` (новый Input System).
- **Игроки:** `PlayerService` (ожидание/реестр локального игрока).
- **Камера:** `CameraService`.
- **Эффекты:** `VfxService`, `SfxService` (пулы; озвучка/частицы по `VfxId`/`SfxId`), `EnemyEffectBindings`, `CameraShakeBindings` (сигналы → эффекты).
- **Враги:** `EnemyTargetingService` + `TargetingMath` (выбор цели: пушка/игрок).
- **Настройки:** `SettingsService` (громкость через `AudioListener.volume` + чувствительность мыши, сохранение в `PlayerPrefs`).
- **Конфиги/сцены:** `ConfigDataProvider` (загрузка SO-конфигов), `SceneLoaderService`.
- **Раунд:** `RoundService` (бэкенд раунда, win/lose).
- **Фабрики:** `GameObjectFactory`.

## 5. Игровая логика без сети (плейн-C# / Mono)
- **ИИ врага** (`Gameplay/World/Enemies/AI/`): `EnemyBrain`, `EnemyContext`, `EnemyStateMachine`, состояния `PursueState`/`PounceState`/`LatchedState`/`DeadState`, `SurfaceCrawler` (ползание по поверхностям без NavMesh), `PhysicsSurfaceProbe`, `SurfaceSensor`, `EnemyAIContracts`.
- **Визуал паука** (`Mimic/`): `Mimic`, `Leg`, `MimicVisualDriver` (процедурные ноги-сплайны, «цепляние» за кости игрока).
- **Движение игрока** (`Player/Movement/`): `MovementCalculator`, `JumpController`, `GroundProbe`, `MovementSnapshot`, `StepCadence` (тестируемая математика).
- **Анимация** (`Player/Animation/`): `PlayerAnimator`, `PlayerHandIK`, `IkWeightController`, `AnimatorStateResolver`, `HandSide`.
- **Переноска (поза)** (`Player/Carry/`): `CarrySolver`, `CarryHold`.
- **Пушка (детали)**: `WeaponBase`, `WeaponModulePart`, `Harpoon`, `HarpoonRope`.
- **Предметы/эффекты**: `PlayerItemPhysics`, `DrunkPostFx`.
- **Спавн-маркеры**: `InteractableSpawnMarker`, `PlayerSpawnArea`, `SpawnPoint`.

## 6. UI (MVP: View + Presenter)
По экрану — пара View+Presenter: **MainMenu**, **Room** (комната лобби), **HUD** (+ `CannonHudPanel` — состояния модулей пушки), **GameOver**, **Pause**, **Settings**, Connect (в простое). Общее (`UI/Common/`): `WindowView`/`WindowView<T>`, `IPresenter`/`EmptyPresenter`, `LoadingBarView` (прогресс-бар), `UIButtonAnimator`, `UITween`, `UIPanelPop`, `PauseMenuController`.

## 7. Данные и сигналы
- **`Data/` (CoOp.Data):** конфиги-`ScriptableObject` (`WindowsConfig`, `WeaponConfig`, `VitalsConfig`, `MovementConfig`, `CarryConfig`, `AnimationConfig`, `NetworkConfig`, `VfxCatalog`, `SfxCatalog`, `InteractableItemConfig`), перечисления (`WindowID`, `SourceState`, `RoundOutcome`, `PlayerLifeState`, `VfxId`, `SfxId`), `Platform` (детект dedicated-сервера), `ILocalPlayer`, пути (`ScenePaths`/`ConfigPaths`).
- **`Signals/`:** свой `SignalBus` + наборы сигналов (`NetworkSignals`, `RoundSignals`, `PlayerLifeSignals`, `EnemySignals`, `LobbySignals`, `ItemSignals`, `WorldSignals`).

## 8. Редакторные инструменты (`EditorTools`, только редактор)
`CoOpToolsMenu` (меню сборки/запуска), `CarryTunerWindow` (настройка поз переноски), `PlayerPrefabTools`, гизмо/оверлеи спавна и пр. В рантайм-сборку не входят.
