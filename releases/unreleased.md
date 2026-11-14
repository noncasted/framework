# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### Bindings: класс уже сгенерирован в другой папке

Кого касается: клиент, игры с префабами или сценами, чьи `*Bindings.g.cs` лежат не в `Generated` той сборки, куда их сейчас
определяет генератор (префаб перенесён в папку другой сборки, файл bindings перенесён руками).

Раньше генератор молча писал второй файл того же класса (CS0101 / два класса с одним именем), теперь отказывает с ошибкой
`... is already generated into <файл>, and this object resolves to <новый путь>`, а Regenerate All падает с
`N bindings failed to regenerate`.

Проверка: `Tools/Framework/Codegen/Regenerate All` (`-executeMethod Internal.RegenerateAll.Run`), затем
`grep -n "is already generated into" client/Logs/*.log` (или лог редактора). На каждую строку: вернуть ассет под сборку файла
(`git mv` вместе с `.meta`) либо перенести файл `*.g.cs` с его `.meta` по пути из ошибки.

Заодно Regenerate All заново привязывает bindings с пустой ссылкой (`{fileID: 0}`), у которых код не менялся: в логе
предупреждение `holds N empty references: binding again`, префаб пересохраняется. Действий не требует, изменённые префабы закоммитить.

### Bindings: Regenerate All заканчивается валидатором

Кого касается: клиент, все игры с bindings.

После прохода привязки Regenerate All прогоняет `HierarchyBindingsValidator` по всем префабам с bindings и загруженным сценам.
Раньше валидатор вызывался только из `RegeneratePrefabs` и из меню, поэтому старые находки (вариант убрал компонент базы, пустая
ссылка, устаревшая структура, bindings не в списке view) теперь появятся ошибками в консоли; последняя строка
`Regenerate All: N prefabs and M scenes validated in T ms, E validation errors` - ошибка при `E > 0`.

Проверка: Regenerate All, в batchmode или без фокуса редактора затем `HierarchyBindingsGenerator.PumpPending()` до `Idle.`;
`grep -n "HierarchyBindingsValidator\|validation errors" <лог редактора>`. Каждую находку исправить, как сказано в её тексте
(Regenerate в ассете, Scan на view, вернуть компонент в варианте). Скрипты игры, которые ждут конца Regenerate All по
`HierarchyBindingsGenerator.HasPending()`, учитывают, что он остаётся `true` до этой проверки.

## Окно реконнекта у сессии хоста-клиента

Кому: играм с P2P-матчами (`LocalSessionServer` как хост матча). Действие необязательно, без него поведение прежнее.

- `LocalSessionServer.Create(id, type, maxUsers, services, reconnectWindow)`: при ненулевом окне потеря соединения гостя без
  `Leave` не завершает его слот: остальные получают `RemoteAway`, гость с билетом того же пользователя возвращается в слот со
  своими сущностями (`LocalUpdate.IsReturn`, `SeatEntities`), по истечении окна слот завершается (`RemoteDisconnect`).
- `SessionCore(..., isReconnectable)`, `SeatAway`, `ExpireAway`: то же для своего хоста без `LocalSessionServer`.
- Поиск мест: `grep -rn "LocalSessionServer\|new SessionCore(" client/Assets`.

### Новый модуль match-metrics: игровые данные матча

Кого касается: клиент и протокол игр, которые хотят писать ряды и события матча (модуль необязательный).

Модуль `client/modules/match-metrics` (сборка `Internal.MatchMetrics`, namespace `Global.MatchMetrics`): поток на забег
`IMatchMetrics.Open(lifetime, matchId, IMatchMetricsClock)`, ряды `Counter` / `Gauge` с тегами (`MatchTags`, до 4), события
`Event(name, MatchEventField...)`, корзины 1 с часов забега, порции раз в 30 с по мета-сокету, одна в полёте, повтор после
реконнекта. Протокол - `shared/MatchMetrics/SharedMatchMetrics.cs`, теги `NetworkContextTags.MatchMetrics*` 220-223 (диапазон 220-229).

Что сделать игре, которая берёт модуль: `client/Packages/manifest.json` - `"com.noncasted.framework.match-metrics":
"file:../../tools/framework/client/modules/match-metrics"`; в union игры (`grep -n "TelemetryStartupTraceResult"
shared/Protocol/NetworkContextUnion.cs`) - четыре строки `SharedMatchMetrics.Open/Opened/Batch/BatchResult`, затем обновить
golden и хеш протокола; мета-бэкенд регистрируется как `IMatchMetricsSocket`, в его скоупе `AddSocketMatchMetrics()`; офлайн
мета отвечает на `Open` - `Opened { Ok }`, на `Batch` - `BatchResult { Ok }`. Бэкенд приёма - следующим шагом. Игре без модуля -
ничего (теги в её union не обязательны).

### match-metrics на бэкенде: приём и хранилище

Кого касается: игры с модулем `match-metrics` (бэкенд приёма). Игре без модуля - ничего.

Проект `backend/MatchMetrics` (сборка `Framework.MatchMetrics`, namespace `Game.MatchMetrics`, правила - `Contract/CONTRACT.md`):
потоки клиентов по мета-сокету (`IMatchMetricsConnections.Of(session.Lifetime, session.UserId)`: `Open`, `Accept`), парсер с
лимитами (`MatchMetricsBatchParser`), поток сервера (`IMatchMetricsWriter.Open(lifetime, matchId)`), хранилище - 6 своих таблиц
Postgres через `IDbSource` (`MatchMetricsSchema`), не состояния грейнов. Кто может писать в матч - ответ игры (`IMatchMetricsAdmission`).

Что сделать игре:
- csproj мета-гейтвея, deploy setup и тестов - `ProjectReference` на `tools/framework/backend/MatchMetrics/MatchMetrics.csproj`,
  в `.slnx` игры - проект.
- Мета-гейтвей: `builder.AddMatchMetrics()` (или свой `AddMatchMetricsCommands` с командами `SharedMatchMetrics.Open` / `Batch`
  поверх `IMatchMetricsConnections`, id пользователя - из сессии сокета) и `builder.AddMatchMetricsAdmission<T>()` - своя
  реализация `IMatchMetricsAdmission.Check(matchId, userId)` (`MatchMetricsAdmit(IsPlayer, ClosedAt)`); без неё хост не собирает
  команды.
- Deploy setup: `await MatchMetricsSchema.Create(connection)` (рядом с `AuditLogTable.Create`).
- Хост, который пишет поток сервера (игровой сервер): `builder.AddMatchMetricsWriter()`.

### match-metrics на бэкенде: чтение

Кого касается: игры с модулем `match-metrics` (консоль, агенты, история матчей). Игре без модуля - ничего.

`IMatchMetricsReader` (`backend/MatchMetrics/Reading`): потоки матча, словарь рядов, точки матча по группам тегов (шаг 1 / 10 /
60 с, ось часов забега или серверная), итоги матча и между матчами, события матча. GET эндпоинты `/api/match-metrics/...`
(`MapMatchMetricsRead`), контракт чтения - раздел «Reading» в `Contract/CONTRACT.md`. Таблицы те же, схема не менялась.

Что сделать игре: в хосте с чтением (мета-гейтвей) `builder.AddMatchMetricsReader()` и
`app.MapMatchMetricsRead().RequireConsoleToken()` (токен - свой у игры, как у `MapClientTelemetryRead`).

### Бэкенд: владелец соединения ждёт его закрытия, сервер не шлёт запросы клиенту

Кого касается: бэкенд всех игр (meta- и game-шлюзы, свои реализации соединений и копии тестов сети).

`Connection.Run` без подписчика у `Reader` закрывает сокет и бросает исключение. `IConnection.Closed` - задача «сокет закрыт», после
неё владелец выходит из обработчика (иначе Kestrel рвёт сокет вместе с close frame, клиент видит 1006). Серверных запросов к клиенту
больше нет: `WriteRequest`, `Responses`, `OnRequestHandled`, `PendingRequestsCount` удалены. Непринятое сообщение клиента
(не читается, null, ответ клиента) закрывает соединение (`InvalidPayloadData` / `ProtocolError`). Handshake ограничен 16 KiB.

- Meta-шлюз: `grep -rn "connection.Run().NoAwait()" backend/Orchestration`. Сначала подписать dispatcher
  (`_entryPoint.OnConnected` / `UserCommandsDispatcher.Run`), затем `var run = connection.Run();`; после конца сессии
  `lifetime.Terminate()` и `await connection.Closed`, только потом выход из middleware.
- Game-шлюз: `grep -n "user.Lifetime.Listen" backend/Orchestration/GameGateway/SessionGate.cs` - в `Join` после конца lifetime
  пользователя `await ((IHostedUser)user).Socket.Closed`.
- Dispatcher: `grep -rn "reader.Responses\|OnRequestHandled" backend` - удалить подписку на `Responses` и `HandleResponse`.
- Свои `IConnection` / `IConnectionReader` / `IConnectionWriter` (тестовые миры pixel-swarm `SessionTestWorld`, storytale
  `ConstructorTestWorld`): `grep -rn "IConnectionWriter\b\|: IConnection" backend` - добавить `Task Closed`, удалить `Responses`,
  `WriteRequest`, `OnRequestHandled`.
- Копии тестов сети (mines-leader, peace-data-reborn): `grep -rn "WriteRequest\|PendingRequestsCount\|RequestTimeout\|Responses" backend/Tools/Tests`
  - удалить тесты серверных запросов; нечитаемый payload теперь закрывает соединение.

### Бэкенд: `OperationProgress` ушёл из framework

Кого касается: mines-leader, pixel-swarm, peace-data-reborn (бенчмарки, `Console/Infrastructure/Benchmark/BenchmarkEntry.razor`).

`grep -rln "IOperationProgress\|OperationProgress\|OperationStatus" backend` - перенести копию `IOperationProgress` /
`OperationStatus` / `OperationProgress` (из `git show v0.6.0:backend/Common/Extensions/OperationProgress.cs` framework) в
`Tools/Benchmarks/Common` игры, в сборку, которую видит её консоль. При переносе: `Release` семафора в `finally`, `Set` вне lock
(иначе подписчик, который бросает, вешает следующий `SetStatus`).

### Бэкенд: резервирование места в сессии возвращает причину

Кого касается: бэкенд всех игр с сессиями.

`grep -rn "TryReserve\|IsHeldOnlyBy" backend --include=*.cs`. `session.TryReserve(id)` -> `session.Reserve(id)`, результат
`SessionReservation` (`Reserved`, `Sealed`, `Closed`, `Full`, `HeldOnlyByOwnStale`): `== true` -> `== SessionReservation.Reserved`;
моки - `.Reserve(...).Returns(SessionReservation.X)`; `SessionAdmission.TryReserve(id, max, now)` -> `Reserve(id, max, now)`;
`ISessionAdmission.IsHeldOnlyBy` удалён. `HeldOnlyByOwnStale` - сессия, которую держит только зависшее соединение самого
пользователя: она сейчас закроется, лобби отвечает `NotFound`.

`LobbyJoinStatus` переехал в `Game.Lobbies` (`Hosting/Lobbies/LobbyJoinStatus.cs`): `grep -rln "LobbyJoinStatus" backend --include=*.cs`
- добавить `using Game.Lobbies;`. `SessionPayloads` стал `static`.

### Бэкенд: состояния кластера меняются методами, координатор выходит из старого деплоя

Кого касается: бэкенд всех игр (тесты, код, который трогает состояния кластера).

- `grep -rn "IsDevelopment = " backend/Tools/Tests` - удалить `IsDevelopment = true,` (`IServiceEnvironment.IsDevelopment` удалён).
- `grep -rnE "\.Slices\[[^]]+\] =|Slices = \{|Members = new|Entries\.(Add|Remove)|GameFlags\[" backend` - вместо прямой записи
  `SetSlice(id, slice)` / `RemoveSlice(id)`, `ServiceDiscoveryStorageState.Set(overview)` / `Remove(id)`,
  `LobbyRegistryState.Add` / `Remove`, флаги через `SetGameFlag`. Известное место: peace-data-reborn `Tools/Tests/Meta/GameServerBalancerTests.cs:113`.
- Свои `IDeployContext` реализуют `Leave()` (`grep -rn ": IDeployContext" backend`). Заменённый координатор теперь выходит из
  деплоя: всё, что игра повесила на `DeployLifetime`, у него останавливается.
- Список открытых лобби не показывает лобби серверов, которых нет в discovery.

### Бэкенд: метрики и трейсы регистрируют сами фичи, `RequeueStuck` удалён

Кого касается: бэкенд всех игр.

- `grep -rn 'TraceExtensions.AllSources\|AddMeter("Backend")\|BackendMetrics' backend --include=*.cs` - в
  `Orchestration/Extensions/ServiceDefaultsExtensions.cs` убрать `foreach (var source in TraceExtensions.AllSources) ...` и
  `.AddMeter("Backend")`: их регистрируют `AddOrleansUtils`, `AddMessaging`, `AddTaskScheduling`. `BackendMetrics.X` ->
  `<Feature>Telemetry.X` (`TransactionTelemetry`, `SideEffectTelemetry`, `DurableQueueTelemetry`, ...). Имена метров и
  инструментов прежние.
- storytale-workshop: `grep -rn 'TraceExtensions.PlayerEndpoints' backend --include=*.cs` - свой `ActivitySource("Player.Endpoints")`
  в игре, `builder.AddTraceSource(source)`; `.Start(name)` остался в `Common.Extensions`.
- `grep -rn 'RequeueStuck()' backend --include=*.cs` - удалить `await _sideEffectsStorage.RequeueStuck();` из
  `Orchestration/Coordinator/ClusterCoordinator.cs` (он возвращал в очередь эффекты, которые silo выполняли, и они выполнялись
  дважды; записи упавшего worker-а возвращает сам `SideEffectsWorker` через `RequeueStuckOlderThan`). В
  `Tools/Tests/State/SideEffectTests.cs` -> `RequeueStuckOlderThan(TimeSpan.Zero)`.
- Удалены `CommonGrain`, `OrleansUtils.Iterate`, `AsSetupLoopStage`, `PostgresJsonbConverter`, `AddEnvironmentOptions`: игры их не используют.
- `DurableQueue`: повтор ждёт наблюдателя, у которого доставка упала; если он не вернулся, сообщение уходит в dead letter после
  `MaxRetryCount`. `AddressableState.SetValue` повторяет publish сохранённого значения. `IsOrleansStarted` становится `true` на
  стадии `Active`. `StateStorageReader.ReadPage` бросает вместо пустой страницы.

### Консоль: `AddConsoleInfrastructure`, таблица аудита из framework

Кого касается: игры с консолью (mines-leader, peace-data-reborn, pixel-swarm, storytale-workshop, gamejam-autobattler).

- `grep -rn "AuditLogStorage\|HeapReportStorage\|HeapReportCollector\|SideEffectsAggregatorService" backend/Orchestration/ConsoleGateway/Program.cs`
  - шесть строк регистрации заменить на `builder.AddConsoleInfrastructure(new ConsoleInfrastructureOptions { CoordinatorConfigFolder = ... })`.
  Папку конфигов координатора для кнопки Write JSON игра передаёт сама (прежнее значение -
  `Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Coordinator")`, например только в Development); без неё кнопки нет.
- `grep -rln "audit_log" backend/Tools/DeploySetup` - удалить `AuditLogSetup.cs`, в `PostResourcesSetup` вызвать
  `await AuditLogTable.Create(connection)`.
- `grep -n "downloadFileFromBase64" backend/Orchestration/ConsoleGateway/App.razor` - удалить функцию: Monitoring импортирует
  `_content/Console/file-download.js` сам.
- `IAuditLogStorage.Write` / `GetPage` бросают при сбое базы, конструктор `AuditLogStorage` без logger:
  `grep -rn "IAuditLogStorage" backend/Console` - на своих страницах try/catch с toast или `AuditLog.Report(...)`.
- Удалены `OperationView.razor`, `HomeGridButton.razor`, `IHeapReportStorage.TryGet`: игры их не используют.

### match-metrics: имена и лимиты в shared, поток сервера после конца бросает

Кого касается: игры с модулем `match-metrics` (pixel-swarm).

- `grep -rn "MatchMetricsLimits\.\|MatchMetricNames\." client/Assets backend` - `Shared.SharedMatchMetricsLimits` /
  `Shared.SharedMatchMetricNames` (`IsName`, `IsKey`, `IsValue`); клиентский `MatchMetricsLimits`, `MatchMetricNames.IsValid` и
  серверный `Game.MatchMetrics.MatchMetricNames` удалены. Типы хранения и парсер бэкенда стали `internal`.
- `grep -rn "IMatchMetricsServerStream" backend` - `Event` / `Gauge` после конца lifetime потока бросают: не писать после конца сессии.
- Парсер отклоняет (`Rejected`) пересекающиеся чанки одного слота и ряд, объявленный в двух слотах.
