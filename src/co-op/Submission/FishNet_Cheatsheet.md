# «Хранители неба» — шпаргалка по сети (FishNet) и механикам

Кооп-шутер от 1-го лица на 2 игроков. Сеть — **FishNet**, транспорт **Tugboat (LiteNetLib, UDP)**, порт по умолчанию **7778**.

---

## 0. Модель в двух словах
- **Геймплей считает СЕРВЕР** (server-authoritative): враги, Источник, пушка, заряд, раунд, здоровье. Клиентам он раздаёт состояние через **`SyncVar`** и **`ObserversRpc`**.
- **Движение игрока считает ВЛАДЕЛЕЦ** (owner-authoritative): клиент-хозяин двигает `CharacterController`, позиция летит через клиент-авторитетный **`NetworkTransform`**, остальные интерполируют.
- **Лобби** — на **FishNet Broadcast** (без заспавненного `NetworkObject`).
- **Сеть → игра:** сетевые события превращаются в локальные сигналы (`SignalBus`) через `NetworkEventBridge`/доменные сервисы.

---

## 1. Подключение игроков

### 1.1. Точки старта (`SessionService`)
| Метод | Что делает | Когда |
|---|---|---|
| `StartServerOnlyAsync(port)` | Поднять сервер БЕЗ клиента (`IsServerOnly`) | Dedicated Server (Linux), автоматически на старте |
| `JoinAsync(addr, port)` | Подключиться клиентом | Оба игрока |
| `StartHostAsync(port)` | Listen-server (сервер+клиент) | Локальный тест / хост-модель |

`SessionService` хранит состояние (`Disconnected/StartingServer/StartingClient/Connected/...`), список клиентов (из `ServerManager.Clients` + событие `OnRemoteConnectionState`), кидает сигналы `ClientConnected/Disconnected/ConnectionLost/Failed`.

### 1.2. Загрузочный поток
```
EntryPoint.Start
  → BootstrapState (грузит конфиги через IConfigDataProvider)
      ├─ если Platform.IsDedicatedServer (UNITY_SERVER или -batchmode):
      │     StartServerOnlyAsync(7778) → LobbyState (БЕЗ UI, сервер ждёт клиентов)
      └─ иначе (клиент/редактор):
            LoadMainMenuState → MainMenuState (меню)
```
`Platform.IsDedicatedServer`: в РЕДАКТОРЕ всегда `false` (выбираешь режим вручную); в БИЛДЕ — `true`, если есть дефайн `UNITY_SERVER` (сборка Dedicated Server) или запуск с `-batchmode`.

### 1.3. Меню и `NetworkConfig.UseDedicatedServer`
Роутинг в `MainMenuPresenter.EnterGame(dedicated)`:
- **`UseDedicatedServer = false` (по умолчанию, ЛОКАЛЬНО):** «Создать комнату» → `StartHostAsync`, «Присоединиться» → `JoinAsync(127.0.0.1)`, и **обе** кнопки идут сразу в **`LoadGameState`** (лобби пропускается — быстрый локальный тест).
- **`UseDedicatedServer = true` (ДЕДИК):** обе кнопки → `JoinAsync(DefaultAddress)` → **`LobbyState`** (комната; «создатель» запускает игру).

`NetworkConfig` (`Resources/Configs/Network`): `DefaultAddress` (IP сервера на клиентах), `DefaultPort` (7778), `UseDedicatedServer`, `ConnectTimeoutSec` (10с).

### 1.4. Лобби (комната) — на Broadcast
- `LobbyService` (project-scoped, NonLazy) строит список участников из **`ServerManager.Clients`** (НЕ из `OnRemoteConnectionState` — оно пропускает clientHost).
- Сообщения — **FishNet Broadcast** (`struct : IBroadcast`, без NetworkObject):
  - клиент → сервер: `SetNicknameBroadcast`, `RequestStartBroadcast`;
  - сервер → клиентам: `LobbyStateBroadcast` (участники + `LeaderClientId`), `GameStartingBroadcast`.
- **Лидер = наименьший ClientId** среди подключённых (унифицирует host: clientHost id 0 = лидер, и dedicated: первый клиент = лидер).
- **Старт:** только лидер → `RequestStartBroadcast` → сервер валидирует (`ServerCanStart`: отправитель == лидер) → рассылает `GameStartingBroadcast` + локально поднимает `LobbyGameStartingSignal` (для headless-сервера) → все идут в `LoadGameState`.
- **Без Ready-кнопки:** все, кто в комнате, считаются готовыми.
- Окно: `UI/Room/RoomView`+`RoomPresenter` (`WindowID.Room`). Слоты: host = создатель, client = 2-й игрок; своё поле — инпут, чужое — ник; панель ожидания пока 2-го нет.

### 1.5. Спавн и сцена
- **Сцена:** сервер `LoadGlobalScenes(GAME_SCENE, ReplaceAll)`, клиенты `WaitForSceneLoaded`. `NetworkEventBridge` поднимает `GameStartedSignal` при загрузке игровой сцены.
- **Игрок:** `PlayerSpawnService` (сервер) спавнит игрока на каждое подключение — слушает `OnRemoteConnectionState` И проходит по `ServerManager.Clients` (событие ненадёжно для clientHost).
- **Локальная регистрация:** `PlayerNetwork.OnStartClient` (только owner) → `IPlayerService.RegisterLocalPlayer`; потребители ждут `await WaitForLocalPlayerAsync(ct)`.
- **Объекты уровня:** `MarkerBasedSpawnService` (сервер) спавнит по маркерам сцены → `LevelReadySignal`.

### 1.6. Как запустить
**Dedicated Server (Linux):**
```bash
chmod +x LinuxServer.x86_64
tmux new -d -s coop "cd ~/путь && ./LinuxServer.x86_64 -batchmode -nographics -logFile ./server.log"
ss -ulnp | grep 7778        # должен слушать 0.0.0.0:7778
# открыть UDP 7778 в фаерволе (ufw + облако)
```
**Клиенты:** Windows-билд (или редактор), `NetworkConfig.DefaultAddress = IP сервера`, `UseDedicatedServer = true`. Первый подключившийся = создатель.

---

## 2. FishNet: базовые приёмы (как используются здесь)

### 2.1. Сетевой объект + DI
Все сетевые объекты наследуют **`Gameplay.Net.InjectableNetworkBehaviour`** (а не голый `NetworkBehaviour`). Он в `OnStartNetwork` (до `OnStartServer/Client`) внедряет зависимости через **сцен-контейнер**, найденный по `ISceneDiContainerRegistry`. Правила:
- новый сетевой префаб → наследуй `InjectableNetworkBehaviour`, обычный `[Inject]`;
- если переопределяешь `OnStartNetwork` — сначала вызови `base`;
- **НЕ** читай `[Inject]` в `Awake`/`OnEnable`;
- никаких `GameObjectContext` на префабах.

### 2.2. SyncVar — состояние сервер → клиенты
```csharp
private readonly SyncVar<float> _netPitch = new(0f);   // FishNet 4: SyncVar<T>
...
_netPitch.Value = x;                                   // пишет ТОЛЬКО сервер
float v = _netPitch.Value;                             // читают все
_netPitch.OnChange += (prev, next, asServer) => {...}; // реакция на изменение
```
Используется в: `PlayerLookController` (питч), `Weapon` (`CorpsesLoaded`), `WeaponModuleSlot` (`IsOccupied`/`MobCount`), `Source` (`State`), `RoundNetworkController` (`RoundOutcome`), `Carryable`/`Drinkable` (захват), `PlayerVitals`/`PlayerDrunk`, `Enemy` (id вцепившегося игрока).

### 2.3. RPC — вызовы между сервером и клиентами
```csharp
[ServerRpc]                       // клиент-владелец → сервер
private void SubmitPitch(float p) => _netPitch.Value = p;

[ObserversRpc]                    // сервер → все клиенты (эффекты/события)
private void RpcPlayFx(Vector3 at) { ... }
```
- `[ServerRpc]`: `PlayerLookController` (питч), `PlayerMelee` (удар), `PlayerDrink`, `PlayerCarry` (запросы захвата), `Weapon` (выстрел).
- `[ObserversRpc]`: `Weapon` (вспышка/трассер выстрела), `Enemy` (эффекты спавна/пунша/латча/смерти), `Source` (события), `RoundNetworkController`, `PlayerCarry` (fx подбора/броска).

### 2.4. Broadcast — без NetworkObject (лобби)
```csharp
// сервер регистрирует:
nm.ServerManager.RegisterBroadcast<RequestStartBroadcast>(OnRequestStart, requireAuthentication:false);
// клиент регистрирует:
nm.ClientManager.RegisterBroadcast<LobbyStateBroadcast>(OnLobbyState);
// отправка:
nm.ClientManager.Broadcast(new RequestStartBroadcast());      // клиент → сервер
nm.ServerManager.Broadcast(new LobbyStateBroadcast {...});    // сервер → всем
```
Списки строим из `ServerManager.Clients` (а не из `OnRemoteConnectionState`).

### 2.5. NetworkTransform
- **Движение игрока:** клиент-авторитетный (owner двигает, синк позиции). 
- **Перенос предметов:** пока предмет «в руках» — `NetworkTransform` ВЫКЛ, держим клиентским «пином»; на сервере — захват/отпускание.

### 2.6. Spawn / Despawn
`NetworkSpawnService` оборачивает `ServerManager.Spawn/Despawn`. Спавн всегда на сервере; клиенты получают копии. Враги, трупы, предметы спавнятся сервером.

### 2.7. Сеть → локальные сигналы
`NetworkEventBridge` + доменные сервисы переводят сетевые события в **`SignalBus`** (свой лёгкий локальный, НЕ Zenject Signals). UI/эффекты подписаны на сигналы, не на сеть напрямую.

---

## 3. Механики — кто считает и через что синкается

| Механика | Скрипт | Авторитет | Сетевые компоненты |
|---|---|---|---|
| Движение | `PlayerMovement` | owner | `NetworkTransform` (client-auth) |
| Взгляд/питч | `PlayerLookController` | owner→server | `SyncVar` питч + `[ServerRpc]` |
| Камера (1-е лицо) | `PlayerCameraRig` | только owner | — (owner-gated) |
| Пушка/гарпун | `Weapon` | server | `SyncVar CorpsesLoaded` + `[ServerRpc]`/`[ObserversRpc]` |
| Заряд трупами | `CorpseIntake`→`Weapon.AddCorpse` | server | триггер на сервере |
| Модули пушки | `WeaponModuleSlot` | server | `SyncVar IsOccupied/MobCount`, «бюджет хвата» → эжект |
| Враг-паук | `Enemy` + AI (`EnemyBrain`/`SurfaceCrawler`/состояния) | server | FSM на сервере, `SyncVar` (latch player), `[ObserversRpc]` эффекты |
| Источник + волны | `Source` | server | `SyncVar State`, спавн волн, `[ObserversRpc]` |
| Здоровье/нокдаун/спасение | `PlayerVitals` | server | `SyncVar` состояние; `ServerKnockDown`/`ServerRevive`; спасение = убить вцепившегося |
| Ближний бой | `PlayerMelee` | server | `[ServerRpc]`, бьёт по ВИДИМОЙ позиции (`Enemy.HitCenter`), не по коллайдеру |
| Перенос предметов | `PlayerCarry` + `Carryable` | server grab + client pin | `SyncVar` захват, `[ServerRpc]`/`[ObserversRpc]`, NetworkTransform off |
| Выпивка/опьянение | `PlayerDrink` + `PlayerDrunk` | server | `[ServerRpc]`, `SyncVar` интенсивность → камера/пост-фх |
| Итог раунда | `RoundNetworkController` | server | `SyncVar RoundOutcome` → `GameEndedSignal` → GameOver |
| Эффекты VFX/SFX | `VfxService`/`SfxService` + `*EffectBindings` | локально на клиентах | сигналы/`ObserversRpc` → проигрывание |

Условие победы: пушка заряжена (`IsCharged`) + Источник открыт → `Weapon.ServerFire` бьёт Источник → `Source` уничтожен = победа. Поражение: все игроки выбыли.

---

## 4. Важные подводные камни (gotchas)

- **NetworkManager — в КОРНЕ сцены.** В Unity 6 `DontDestroyOnLoad` на не-корневом объекте не работает → ломается `TimeManager` → сервер не биндится. `NetworkService.Initialize` принудительно выносит NM в корень.
- **Tugboat Server Bind Address = пусто / `0.0.0.0`** (не `127.0.0.1`), иначе внешние клиенты не достучатся. + открыть **UDP 7778** в фаерволе (ufw + облако).
- **NetworkManager-префаб** инстанцировать в корне сцены (см. выше).
- **Не читать `[Inject]` в `Awake`** на сетевых объектах — только после `OnStartNetwork`.
- **Списки клиентов/лобби — из `ServerManager.Clients`**, не из `OnRemoteConnectionState` (последнее пропускает clientHost).
- **Таймауты — `UniTask.Delay(..., cancellationToken: ct)`** (PlayerLoop/главный поток). НИКОГДА `CancellationTokenSource.CancelAfter` (ThreadPool → исключения Unity API).
- **Звук:** мастер = `AudioListener.volume` (ставит `SettingsService` из PlayerPrefs). `SfxService` подставляет pitch=1, если в каталоге `PitchRange (0,0)` (иначе pitch 0 = тишина).
- **Загрузочный экран** — постоянный объект (`DontDestroyOnLoad`) в сцене Bootstrap; `LoadingScreenService` его включает/выключает; прячется при открытии окна MainMenu.

---

## 5. Файлы-ориентиры
- Сеть: `Infrastructure/Services/Network/` (`NetworkService`, `SessionService`, `NetworkEventBridge`).
- Лобби: `Infrastructure/Services/Lobby/` (`LobbyService`, `LobbyBroadcasts`).
- Спавн: `Infrastructure/Services/Spawn/` (`PlayerSpawnService`, `NetworkSpawnService`, `MarkerBasedSpawnService`).
- Состояния: `Core/States/` (`BootstrapState`, `LoadGameState`, `GameplayState`, ...).
- Сетевые объекты: `Gameplay/Net/InjectableNetworkBehaviour`, `Gameplay/Player/*`, `Gameplay/World/*`.
- Конфиг сети: `Data/Configs/NetworkConfig`, `Data/Platform`.
- Авторитетная документация по дизайну/сети: `CLAUDE.md` + `GDD.md`.
