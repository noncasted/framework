# Шаги для releases/unreleased.md (собираются из отчётов агентов)

## Шаг 4 Hosting/Session
1. Все игры: `grep -rn "TryReserve" backend --include=*.cs`. `session.TryReserve(id)` -> `session.Reserve(id)` возвращает `SessionReservation` (Reserved/Sealed/Closed/Full/HeldOnlyByOwnStale); `== true` -> `== SessionReservation.Reserved`; моки `.Reserve(...).Returns(SessionReservation.X)`; `SessionAdmission.TryReserve(id,max,now)` -> `Reserve(...)`; `ISessionAdmission.IsHeldOnlyBy` удалён. HeldOnlyByOwnStale = закрывающаяся сессия.
2. Игры с `LobbyJoinStatus` фреймворка (pixel-swarm): `grep -rln "LobbyJoinStatus" backend --include=*.cs` -> `using Game.Lobbies;`; `SessionPayloads` теперь static.
CLAUDE.md Sessions/Hosting: «`ISession.Reserve(userId)` returns `SessionReservation` (the refusal reason; `HeldOnlyByOwnStale` - a closing session, answered NotFound)».

## Шаг 5 ClientTelemetry
- (не нужен для игр) `TelemetryBatchParser.ParseDevice/ParseTags/ParsePrevious` убраны -> `TelemetryTagsParser.Parse`, `TelemetryPreviousParser.Parse`; `IClientTelemetryReader.GetSampleWindow` добавлен.

## Шаг 6 MatchMetrics (BREAKING)
- Клиент и бэкенд игр: `grep -rn "MatchMetricsLimits\.\|MatchMetricNames\." client/Assets backend` -> `Shared.SharedMatchMetricsLimits` / `Shared.SharedMatchMetricNames` (`IsName`, `IsKey`, `IsValue`); `Global.MatchMetrics.MatchMetricsLimits`, клиентский `MatchMetricNames.IsValid`, `Game.MatchMetrics.MatchMetricNames` удалены.
- Типы хранения и парсер MatchMetrics стали internal (игры не используют).
- Серверы (pixel-swarm): `grep -rn "IMatchMetricsServerStream" backend` - `Event`/`Gauge` после конца lifetime бросают.
- Unity-проверка compile нужна (shared/MatchMetrics, client/modules/match-metrics).

## Шаг 7 Console (BREAKING)
1. Игры с консолью: `grep -rn "AuditLogStorage\|HeapReportStorage\|HeapReportCollector\|SideEffectsAggregatorService" backend/Orchestration/ConsoleGateway/Program.cs` -> заменить 6 строк на `builder.AddConsoleInfrastructure(new ConsoleInfrastructureOptions { CoordinatorConfigFolder = ... })` (прежний путь `Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Coordinator")` передать явно; без него кнопки Write JSON нет).
2. `grep -rln "audit_log" backend/Tools/DeploySetup` -> удалить `AuditLogSetup.cs`, в `PostResourcesSetup` вызвать `await AuditLogTable.Create(connection)`.
3. `grep -n "downloadFileFromBase64" backend/Orchestration/ConsoleGateway/App.razor` -> удалить (Monitoring импортирует `_content/Console/file-download.js`).
4. `IAuditLogStorage.Write/GetPage` бросают при сбое БД; конструктор `AuditLogStorage` без logger. `grep -rn "IAuditLogStorage" backend/Console` -> try/catch с toast или `AuditLog.Report(...)`.
Доки: BLAZOR.md строка 5 упоминает удалённый OperationView.razor; CLAUDE.md Console: `AddConsoleInfrastructure(ConsoleInfrastructureOptions)` и `AuditLogTable.Create`.

## Шаг 3 Common (BREAKING)
1. Meta-шлюз (mines-leader, pixel-swarm, peace-data-reborn, storytale): `grep -rn "connection.Run().NoAwait()" backend/Orchestration`. Сначала подписать dispatcher (`_entryPoint.OnConnected` / `UserCommandsDispatcher.Run`), затем `var run = connection.Run();`; после конца сессии `lifetime.Terminate()` и `await connection.Closed`, только потом выход из middleware. `Run` без подписчика Reader закрывает сокет и бросает.
2. Game-шлюз: `grep -n "user.Lifetime.Listen" backend/Orchestration/GameGateway/SessionGate.cs` - в `Join` после конца lifetime пользователя ждать `((IHostedUser)user).Socket.Closed`.
3. Dispatcher meta: `grep -rn "reader.Responses\|OnRequestHandled" backend` - удалить подписку на `Responses` и `HandleResponse`.
4. Свои IConnection/IConnectionReader/IConnectionWriter (тестовые миры pixel-swarm `SessionTestWorld`, storytale `ConstructorTestWorld`): `grep -rn "IConnectionWriter\b\|: IConnection" backend` - добавить `Task Closed`, удалить `Responses`, `WriteRequest`, `OnRequestHandled`.
5. Копии тестов framework (mines-leader, peace-data-reborn `Tools/Tests/Network/ConnectionWriterTests.cs`, `ConnectionReaderTests.cs`): `grep -rn "WriteRequest\|PendingRequestsCount\|RequestTimeout\|Responses" backend/Tools/Tests` - удалить; нечитаемый payload закрывает соединение.
6. OperationProgress (mines-leader, pixel-swarm, peace-data-reborn: бенчмарки и `Console/Infrastructure/Benchmark/BenchmarkEntry.razor`): `grep -rln "IOperationProgress\|OperationProgress\|OperationStatus" backend` - перенести копию в `Tools/Benchmarks/Common` (Release в finally, подписчики вне lock).
CLAUDE.md Network/Sessions: владелец `Connection` подписывает Reader до `Run`, выходит после `Closed`; сервер не шлёт запросы клиенту; непринятое сообщение закрывает соединение; handshake 16 KiB.

## Шаг 2 Cluster (BREAKING)
1. Тесты игр: `grep -rn "IsDevelopment = " backend/Tools/Tests` - удалить `IsDevelopment = true,` из `new ServiceEnvironment { ... }`.
2. `grep -rnE "\.Slices\[[^]]+\] =|Slices = \{|Members = new|Entries\.(Add|Remove)|GameFlags\[" backend` -> `data.SetSlice(id, slice)` / `RemoveSlice(id)`, `ServiceDiscoveryStorageState.Set(overview)` / `Remove(id)`, `LobbyRegistryState.Add/Remove`, флаги через `SetGameFlag`. Известно: peace-data-reborn `Tools/Tests/Meta/GameServerBalancerTests.cs:113`.
3. Свои `IDeployContext` реализуют `Leave()` (`grep -rn ": IDeployContext" backend`, сейчас нет).
4. `new OnlineTracker(` требует `IDeployContext` (сейчас нет).
CLAUDE.md Cluster: `IDeployContext.Leave()` - заменённый координатор выходит из деплоя, всё на `DeployLifetime` (OnlineTracker, слушатели каналов) останавливается; строки деплоя пишутся через `DeploymentStateRow.Change`.

## Шаг 1 Infrastructure
- Все игры: `grep -rn "RequeueStuck()" backend/Orchestration` - в `Orchestration/Coordinator/ClusterCoordinator.cs` удалить `await _sideEffectsStorage.RequeueStuck();` и лог перед ним (возвращал в очередь выполняющиеся эффекты; записи упавшего worker-а возвращает `SideEffectsWorker` через `RequeueStuckOlderThan`).
- Удалены `CommonGrain`/`ICommonGrain`, `OrleansUtils.Iterate`, `AsSetupLoopStage`, `StateStorageCache.GetWriteQuery/GetDeleteQuery` (игры не используют).
- DurableQueue: наблюдатель, упавший и не вернувшийся, доводит сообщение до dead letter после MaxRetryCount.

## Шаг 9 Метрики/трейсы, RequeueStuck (BREAKING)
1. Все игры с backend: `grep -rn 'TraceExtensions.AllSources\|AddMeter("Backend")\|BackendMetrics' backend --include=*.cs` - в `Orchestration/Extensions/ServiceDefaultsExtensions.cs` убрать `foreach (var source in TraceExtensions.AllSources) tracing.AddSource(source.Name);` и `.AddMeter("Backend")` (их регистрируют `AddOrleansUtils`, `AddMessaging`, `AddTaskScheduling`); `BackendMetrics.X` -> `<Feature>Telemetry.X`.
2. storytale-workshop: `grep -rn 'TraceExtensions.PlayerEndpoints' backend --include=*.cs` - свой `ActivitySource("Player.Endpoints")` в игре, регистрация `builder.AddTraceSource(...)`; `.Start(name)` остаётся; поправить `Tools/Tests/Security/DeviceSignInTests.cs`.
3. `RequeueStuck` удалён: `grep -rn 'RequeueStuck()' backend --include=*.cs` - убрать из `ClusterCoordinator.cs`; в `Tools/Tests/State/SideEffectTests.cs` -> `RequeueStuckOlderThan(TimeSpan.Zero)`.
