# GDD — кооп тёмное фэнтези (актуально, 2026-06)

> Описывает **реально реализованную** игру по состоянию кода. Игра — **от первого лица** (исходное «третье лицо» устарело). Код меняли несколько человек; значения ниже — текущие в ассетах конфигов, их можно тюнить. Пометки: `[готово]`, `[изменено]`, `[не реализовано]`, `[режется]`.

## Жанр и формат
Кооп от первого лица на **двух игроков** `[готово]`. Сессия — раунд-волны на локации. Сеттинг — тёмное фэнтези. Прогрессия между локациями `[не реализовано]`.

## Нарратив
Двое странников чинят защитное орудие (пушку) из найденных деталей и уничтожают **Источник** угрозы. Роли гибкие — любой может собирать, заряжать, стрелять, бить. Переход на новые локации/прогрессия — `[не реализовано]`.

---

# Подключение и мультиплеер

**Сеть: FishNet, транспорт Tugboat (LiteNetLib, UDP).** Порт по умолчанию **7778**, адрес — из `NetworkConfig` (`Resources/Configs/Network`), таймаут коннекта 10 с.

### Модель (целевая): Linux Dedicated Server
- **Выделенный headless-сервер** запускает только серверную часть: `SessionService.StartServerOnlyAsync(port)` (есть `IsServerOnly`). Сервер авторитетный — крутит всю симуляцию.
- **Оба игрока подключаются как КЛИЕНТЫ** (`SessionService.JoinAsync(адрес, порт)`). Нет «хоста-игрока».
- Listen-server (`StartHostAsync` = сервер+клиент в одном процессе) и **хост-миграция** (`LobbyState.OnConnectionLost → StartHostAsync`, фолбэк `LoadGameState`) — **только для локального теста / ParrelSync двух-редакторного**; для dedicated не нужны `[переходное]`.

### Поток подключения
1. **MainMenu** (`MainMenuPresenter`): **Join** → `JoinAsync(DefaultAddress:Port)` (без окна ввода IP — дормантный `Connect` остался); **Host** → `StartHostAsync` (локальный тест). Оба ведут в `LobbyState`.
2. **Lobby / Room** (`UI/Room`, `WindowID.Room`): на **FishNet Broadcast** (БЕЗ spawned NetworkObject). `LobbyService` строит участников из `ServerManager.Clients`, синкает ник/ready (client→server `SetNickname`/`SetReady`), пушит `LobbyStateBroadcast`/`GameStartingBroadcast` → сигналы `LobbyChanged`/`LobbyGameStarting`. Ник — плейсхолдер до ввода. Start доступен когда один (соло-тест) или все ready (`ILobbyService.CanStart`).
3. **Start** → broadcast `GameStarting` → у всех `LobbyState` → `LoadGameState`: **сервер** грузит `GAME_SCENE` глобально (`LoadGlobalScenes`, ReplaceAll), **клиенты ждут** (`WaitForSceneLoadedAsync`). `NetworkEventBridge` на загрузке сцены шлёт `GameStartedSignal`. После `LevelReadySignal` → `GameplayState`.

### Авторитет
- **Геймплей — серверный** (server-authoritative): Источник, раунд, ИИ врагов, пушка, витал — логика на сервере, состояние через `SyncVar`, события через `ObserversRpc`/Broadcast → локальные `SignalBus`-сигналы у каждого клиента.
- **Движение игрока — owner-authoritative**: владелец читает ввод → двигает `CharacterController` → `NetworkTransform` (client-authoritative) реплицирует; не-владельцы интерполируют.
- **Перенос предметов** — серверный (захват/отпускание через ServerRpc), но держимый предмет **жёстко пиннится в руке на клиенте** (NetworkTransform на время держания выключается, чтобы не «драться»).
- **Спавн игрока**: `PlayerSpawnService` (server-only) спавнит Player на каждое подключение (подписка на `OnRemoteConnectionState` + обход `ServerManager.Clients`). Локальная регистрация: `PlayerNetwork.OnStartClient` (если owner) → `IPlayerService.RegisterLocalPlayer`.
- Все FishNet-объекты (в т.ч. Player) инжектятся через `InjectableNetworkBehaviour` (scene-контейнер) — без GameObjectContext.

---

# Игровая петля (как реализовано)

### 1. Фаза «Сбор» `[готово]`
- По локации лежат **компоненты** — физические `Carryable`. Поднимаешь (зажать E), несёшь, можешь бросить.
- **Источник** виден сразу. Состояния `Gather → Open → Destroyed` (`Source`), пульсирует/звучит. Таймер сбора `WaveSetConfig.GatherDurationSec` (~30 с).
- Разведчики в фазе сбора, тяжесть/хрупкость переноса, двуручный перенос — `[не реализовано]`.

### 2. Сборка пушки `[готово]`
- Принесённые компоненты вставляются в **сокеты** пушки (`WeaponModuleSlot`) — отпускаешь у нужного сокета. Сборка **последовательная** (по `_order`: следующий свободный по порядку). Когда все сокеты заняты — `IsAssembled`, летит `WeaponAssembledSignal`. **Одна общая пушка** на двоих.

### 3. Фаза «Волна» (Open) `[готово]`
- Источник переходит в **Open**: становится **уязвимым** и **спавнит мобов** (интервал `SpawnInterval` ~2.5 с, не больше `MaxAliveEnemies` ~15). Отдельной многоволновой системы нет — один «Open»-поток (сигналы Wave* объявлены, но НЕ шлются).
- **Мобы-пауки** (процедурный Mimic, ноги-LineRenderer). ИИ — **surface-crawler** без NavMesh: ползут к пушке по любой поверхности, **перелезают стены/здания**. Цель — пушка; если игрок на пути/рядом — **прыжок (pounce) → присасывание (latch) → нокдаун**.
- **Урон Источнику — только из ЗАРЯЖЕННОЙ пушки** (см. ниже). По мобам урон есть всегда.

### 4. Финал `[готово, портал режется]`
- Источник уничтожен заряженным выстрелом → **победа**. Сейчас в коде после разрушения спавнится **портал** (все живые входят → `PortalEntered` → Victory). **Портал решено убрать** `[режется]` — условие победы станет «Источник уничтожен». `RoundNetworkController` (server, `SyncVar<RoundOutcome>` None/Victory/Defeat) → `GameEndedSignal` → `GameOverState`.
- **Поражение** — когда не осталось ни одного живого игрока (`AllPlayersDownedOrDeadSignal` → Defeat).

---

# Системы

### Пушка / орудие (`Gameplay/World/Weapon`)
- **Заряд трупами (Model B):** убитые мобы роняют **труп** (`Carryable`+`Corpse`). Игроки заносят трупы в приёмник пушки (`CorpseIntake`, триггер) → `CorpsesLoaded++`. Пока `CorpsesLoaded < RequiredCorpses` (≈3) — **пушка вообще не наносит урон Источнику**. Когда заряжена (`IsCharged`) — **точный выстрел уничтожает Источник в любой момент**, пока тот Open (`HitsToDestroy`≈1). Окна уязвимости/`FullOpen` нет.
- **Управление:** подойти к месту оператора (`_operatorStand`, радиус ~1.5) и сесть (`ClientRequestMount`, требует собранную пушку и свободного оператора `OperatorClientId==-1`). Ручной 2-осевой прицел (yaw ±70°, pitch −45..+20°), синк через `_aimYaw/_aimPitch`. Выстрел — гарпун (`ClientFire`→`ServerFire`), кулдаун ~1.5 с, дистанция ~200, урон выстрела ~10; урон применяется после долёта гарпуна.
- **Мобы отрывают модули:** присосавшийся к модулю моб тратит **бюджет хвата** сокета (`_gripBudgetSec` ~30 с, дренаж × число мобов на нём/сек). Бюджет в 0 → `ServerEject()`: модуль отваливается, снова свободный `Carryable` → его подбирают и вставляют обратно. Так пушку постепенно разбирают.

### Источник (`Gameplay/World/Source`)
- SyncVar: `State` (Gather/Open/Destroyed), `OpenAmount`, `IsVulnerable`, `Destroyed`. Серверный async-жизненный цикл: Gather (таймер, без спавна) → Open (уязвим, спавнит мобов) → при достаточном уроне заряженной пушки `Destroyed` (деспавн всех мобов, спавн портала). Сигналы: `SourceStateChanged`/`SourceVulnerable`/`SourceDamaged`/`SourceDestroyed`.

### Враги (`Gameplay/World/Enemies` + `Enemies/Mimic`)
- **Процедурный паук Mimic** (скрипты `Mimic`/`Leg`/`MimicVisualDriver` — **перенесены в CoOp.Main**, чтобы `Enemy` ими управлял; namespace `MimicSpace`). Ноги — LineRenderer; стопа фиксируется, при отходе тела > `maxLegDistance` нога ретрактится и новая ставится впереди. Тюнинг под естественную походку: `CrawlSpeed`≈2.8, плотнее ноги, плавный рост.
- **ИИ:** sync-FSM `Pursue/Pounce/Latched/Dead` + `SurfaceCrawler`. Латч на пушку = к конкретному модулю; латч на игрока = нокдаун. **MaxHealth≈10** (кик 15 = один удар).
- **Впивание в игрока (cling):** при латче на игрока тело паука садится на верх корпуса/спину, а ноги **хватают кости тела** (обе кисти, голова, грудь, плечи, таз — через `Animator.GetBoneTransform`) и едут за ними. Синкается `SyncVar` латч-игрока; каждый клиент собирает кости и кормит `Mimic.SetCling`.

### Витал и спасение (`Gameplay/Player/Vitals`)
- `PlayerLifeState`: Alive/Downed/Dead. Нокдаун (`ServerKnockDown`): роняет, бросает переносимое, камера от 3-го лица, движение/обзор выкл, таймер `DownReviveSeconds`≈15 с.
- **Спасение — убить присосавшегося моба** (кик или выстрел пушки). Смерть мога (`Enemy.ServerDespawn → ReleaseLatchedPlayer`) → `ServerRevive` → игрок встаёт по анимации. **Hold-revive убран** (поля Revive* в конфиге не используются).
- Не спасли за таймер → `Dead` (спектатор за живым напарником). Нет живых → поражение.

### Мили-кик (`Gameplay/Player/Combat/PlayerMelee`)
- ЛКМ (когда НЕ за пушкой). Урон по `Enemy.All` в радиусе перед игроком (по видимой позиции, не по физколлайдеру), ~15 урона, кулдаун ~0.6 с. Убивает мобов на земле, на пушке и **сбивает с поваленного напарника**. `MeleePromptSignal` подсвечивает, когда враг в зоне.

### Выпивка / опьянение (`Gameplay/Player/Combat/PlayerDrink` + `Vitals/PlayerDrunk`)
- Бутылка (`Drinkable`, НЕ carryable) — **зажать E** (промпт «Зажмите E чтобы выпить»): рука тянет бутылку (анимация Drinking, ~3 с), движение блокируется. Рано отпустил — отмена. Допил — бутылка выбрасывается (можно подобрать снова), `ServerAddDrink`.
- **Опьянение** — стакающийся `SyncVar` intensity (+0.6/глоток, макс 2, спад 0.06/с). Эффект: пьяные анимации + камера (качание/крен/FOV в `PlayerCameraRig`) + аддитивный глобальный пост-эффект `DrunkVolume` (chromatic aberration/lens distortion/vignette/DoF), гонимый `DrunkPostFx` от `PlayerDrunk.Local.Intensity`.

### Эффекты (`Infrastructure/Services/Effects`)
- Каталоги `VfxCatalog`/`SfxCatalog` (по `VfxId`/`SfxId`), **пустой слот = тишина/ничего**. Сервисы `IVfxService`/`ISfxService` (пулинг). Биндинги (`World/Player/Enemy EffectBindings`) переводят сигналы в проигрывание. Почти все ID каталога вызываются из кода (кроме `SfxId.PortalIdle` — портал режется). Сами клипы/префабы в ассетах — назначает дизайнер.

### Инструменты дизайнера (editor-only)
- **F9** — debug-консоль (`DebugTestPanel`): спавн/килл мобов, заряд/сборка пушки, нокдаун/ревайв, исход раунда, таймскейл.
- **Tools/CoOp/Carry & Drink Tuner** — окно настройки поз переноса/питья ручками в SceneView (в Play, реальный IK) с запеканием в префабы.

---

# Конфиги (текущие значения, тюнятся в ассетах)
- **Wave/Source:** Gather ~30 с, SpawnInterval ~2.5 с, MaxAlive ~15, HitsToDestroy ~1.
- **Weapon:** RequiredCorpses ~3, ShotDamage ~10, дистанция ~200, кулдаун ~1.5 с, yaw ±70°, pitch −45..+20°.
- **Vitals:** DownReviveSeconds ~15 с.
- **Enemy:** MaxHealth ~10, CrawlSpeed ~2.8, PounceRange ~4, PounceSpeed ~9, LatchDistance ~0.9, AggroRadius ~6.

# Не реализовано / в планах
Прогрессия и переход на локации; разведчики в фазе сбора; двуручный перенос; тяжесть/хрупкость переноса; многоволновая система (сейчас один Open-поток). **Портал — режется** (победа = Источник уничтожен).

# Виральные моменты
Из физики и давления времени: тащишь тяжёлый модуль и не успеваешь; напарник присосан, пока пушку разбирают по модулю; финальный заряженный выстрел мимо; ключевой модуль оторвали в худший момент. Не срежиссировано — следствие систем.
