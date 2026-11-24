# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

## Шаги

### 1. Код лобби после матча: `PassToMatch(…, leaderId, userIds)`, `ILobbyCodes.MarkReturned`, `LobbyOptions.ReturnsAfterMatch` (BREAKING)

Кого касается: бэкенд игр с лобби фреймворка (`Game.Lobbies`: pixel-swarm, neon-dodge; у peace-data-reborn своя копия
`LobbyCodeLeases` - не трогать). Код лобби может вернуться из матча в новое лобби того же сервера с тем же кодом: с
`LobbyOptions.ReturnsAfterMatch = true` один раз на матч - при первом игроке, ушедшем из матча насовсем (разрыв с окном переподключения -
не уход), или при закрытии матча - `ILobbyFactory` игры получает `LobbyCreation { Code = тот же, Return = { LobbySessionId,
MatchSessionId, LeaderId } }`, регистр указывает код на новое лобби (`ILobbyCodes.MarkReturned`), места зарезервированы всем игрокам
матча (`JoinTimeout` лобби), лидер - лидер матча, как только он вошёл (до него - первый вошедший). Отказ фабрики - код освобождается,
матч идёт дальше. Без флага поведение прежнее.
Поиск: `git grep -nE "PassToMatch\(|new LobbyCodeLeases\(|: ILobbyCodes\b|new LobbySessionInfo" -- backend`.
Сделать:
- `ILobbyCodeLeases.PassToMatch(Guid lobbySessionId, ISession match)` -> `PassToMatch(lobbySessionId, match, leaderId, userIds)`:
  реализация `ILobbyMatches.Create` игры (`backend/Game/Global/LobbyMatches.cs`) передаёт лидера и пользователей, которых получила.
- `new LobbyCodeLeases(codes, logger)` -> `new LobbyCodeLeases(codes, afterMatch, logger)`: в тестовых мирах игры
  (`SessionTestWorld`, `GameTestWorld`) `afterMatch` - `new LobbyCodeRelease()` (код освобождается с матчем) или `new LobbyReturns(lobbyFactory,
  lobbyOptions, logger)`. В контейнере сервера правило регистрирует `AddSessionHosting` по флагу.
- Фейки `ILobbyCodes` в тестах игр: новый член `Task MarkReturned(string code, Guid sessionId, Guid returnSessionId)` - запись кода
  переходит сессии `returnSessionId` того же сервера без матча; вызов не держателя кода бросает, как `Release` (образец -
  `backend/Tests/Hosting/FakeLobbyCodes.cs` фреймворка).
- Игра, включившая возврат (`ReturnsAfterMatch = true`): фабрика лобби передаёт `Return = creation.Return` в `LobbySessionInfo` (без
  этого лидером лобби возврата будет первый вошедший) и сама переносит свои свойства комнаты (режим, арену) из своей записи матча
  `Return.MatchSessionId`. Свои фейки `ILobbyFactory` - тоже.

## Проверить поведение

- Сеть: событие объекта, тип которого ещё никто не запросил (`GetEvent<T>`), больше не бросает `Event type ... not found in actions`
  (раньше исключение ловил `NetworkCommandsDispatcher`, событие терялось): `NetworkEvents` держит его в порядке прихода и отдаёт
  первому подписчику источника своего типа (`NetworkEventSource<T>`, первый `Advise` после `GetEvent`), до событий, пришедших позже.
  Предел - `NetworkEvents.MaxPendingEvents = 64` событий без источника на объект, 65-е бросает `InvalidOperationException` с именем
  типа. Правок игр не требует: подписи `INetworkEvents`, `GetEvent`, `InvokeLocal` прежние, источник по-прежнему `ViewableDelegate<T>`.
### Одна фаза скоупа: `IScopeSetup` (BREAKING)

Кого касается: клиент всех игр (pixel-swarm, mines-leader, peace-data-reborn и остальные на этом ядре). Остался один колбэк
скоупа, `IScopeSetup.OnSetup(lifetime)`, синхронный. Удалены `IScopeBaseSetup(Async)`, `IScopeSetupAsync`,
`IScopeSetupCompletion(Async)`, `IScopeLoaded(Async)`, `IScopeDispose(Async)`, `ILoadedScope.Initialize()`,
`IEventLoop.RunCustomAwaited`; `RunConstruct` стал `RunSetup`, `RunCustom<T>` остался. Правила в `COMMON_CONTAINER.md` § Setup:
то, без чего сервисом нельзя пользоваться, делается в его конструкторе (`[Inject]` у MonoBehaviour), `OnSetup` только подписывается,
любой другой порядок пишется вызовом у владельца скоупа.
Поиск: `git grep -nE "IScopeBaseSetup|IScopeSetupAsync|IScopeSetupCompletion|IScopeLoaded|IScopeDispose|RunCustomAwaited|RunConstruct|\.Initialize\(\);|OnBaseSetup|OnSetupCompletion|OnLoaded\(" -- client/Assets ':!client/Assets/Plugins'`
Сделать:
- `IScopeLoaded` (`GameLoop` каждой игры): `OnLoaded(lifetime)` -> публичный `Start(lifetime)`, регистрация `.As<IScopeLoaded>()` ->
  `.AsSelf()`, в загрузчике (`GameLoopScopeExtensions`) `await scope.Initialize()` -> `scope.Resolve<GameLoop>().Start(scope.Lifetime)`.
  Остальные `await scope.Initialize();` (меню, лобби, мета, забег, фон меню) удалить: других слушателей `IScopeLoaded` в играх нет.
- Настройки игры (`Settings` в `Common/Global/Settings`, `TutorialProgress` peace-data-reborn): тело `OnSetup` / `OnSetupAsync`
  (`_saves.Get` из уже загруженных сохранений, `Push`) - в конструктор, интерфейс фазы убрать. Без этого `AudioPlayer` (громкость читается в его `[Inject]`),
  `FrameRateLimit` и `RenderResolution` (их `OnSetup`) получают значения по умолчанию, а сохранённые приходят следом.
- `MetaLoop.OnBaseSetup` (все три игры, подключение через `.Forget()`): -> `IScopeSetup.OnSetup`, тело то же.
- `IScopeBaseSetup` остального кода (pixel-swarm: `GameSounds`, `GameSoundLoops`, `PlayerWeapon`, `WeaponView`; mines-leader:
  `GameOverlay`, `GameInfoOverlay`; peace-data-reborn: `LevelElementWallZones`, `ProceduralLocation`): то, что читают другие сервисы
  (созданные объекты, пулы, начальные значения), - в конструктор, подписки и updater - в `OnSetup`. Пример: `ScreenSafeArea`,
  `GlobalCamera`, `AudioPlayer` фреймворка.
- `IScopeSetupCompletion` pixel-swarm (`ProjectileWorld`, `CoreWorld`, `EnemyPhaseLooks`: ждали мир, который создаёт
  `SwarmSimulation.OnSetup`): мир создавать в конструкторе `SwarmSimulation` (они берут его зависимостью), тогда их код - в `OnSetup`.
- Настоящие ожидания: `RunShaderWarmup` pixel-swarm (`IScopeSetupAsync`) и `StaticLocationLoader` peace-data-reborn
  (`IScopeSetupCompletionAsync`) - публичный метод, который загрузчик забега / уровня вызывает и ждёт после `Load` скоупа, там же,
  где начинается забег.
- Ошибка `CINGR018`: тип реализует `IScopeSetup`, но его регистрация не перечисляет `IScopeSetup` (его `OnSetup` молча не вызывался).
  Добавить `.As<IScopeSetup>()` или убрать мёртвый `OnSetup`.
- `RunCustom<T>` (`IGameStarted`, `IMetaSetupCompleted`, `ILevelSetup`) не меняется.
- Слушатели lifetime сервисов сущности (`lifetime.Listen` в `OnSetup`) теперь срабатывают до освобождения контейнера сущности и её
  asset groups (раньше после): обходные пути вроде отложенного чтения ассетов в конце сущности можно убрать.

Проверить поведение: настройки (громкость, частота кадров, разрешение) на старте сразу берутся из сохранения; меню и забег стартуют
(`GameLoop.Start`); прогрев шейдеров pixel-swarm заканчивается до начала забега.

Без действий: `AddSteam` запускает Steam API сам (`SteamApi.Start`), `SteamClient` только качает колбэки; `GeneratedRoots.Register(MethodInfo)`
в `Internal.TestTools` регистрирует сгенерированный корень в тестах EditMode; `CINGR012` снят.

### Сохранения загружаются явно: `ISaves.Load()`, `AddSaves` (BREAKING)

Кого касается: клиент всех игр с `AddItchSaves` (pixel-swarm, mines-leader, peace-data-reborn, neon-dodge), свои реализации и
фейки `ISaves` (neon-dodge: `FakeSaves` в тестах; storytale-workshop: своя копия `ItchSaves`). Хранилище читается не в конструкторе,
а в `ISaves.Load()`, его ждёт `AddSaves` при сборке скоупа (шаг startup trace, асинхронное хранилище встанет туда же); `Get` и `Save`
до `Load` бросают.
Поиск: `git grep -nE "AddItchSaves|ISaves\b" -- client/Assets ':!client/Assets/Plugins'`
Сделать:
- `builder.AddItchSaves();` -> `await builder.AddItchSaves();`: инсталлер паблишера (`GlobalPublisherExtensions`) становится
  `async UniTask`, его вызов в корне глобального скоупа - `await`.
- Своя реализация `ISaves`: чтение хранилища - в `public UniTask Load()`, регистрация `builder.Register<X>().As<ISaves>()` ->
  `await builder.AddSaves(new X())`.
- Фейки `ISaves` в тестах: добавить `public UniTask Load() => UniTask.CompletedTask;`.
