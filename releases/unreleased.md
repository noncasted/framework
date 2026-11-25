# Not released yet

Steps for the games from the commits after the last tag, the format is in [README](README.md). A new step is added at the end in the same
commit as the change. At a release the steps move to `<version>.md`, only this header stays here.

### 1. BREAKING: the OTel defaults of the hosts come from the framework (`AddTelemetryDefaults`)

Who: backend, every game (pixel-swarm, mines-leader, neon-dodge, peace-data-reborn, gamejam-autobattler).

Search: `git grep -nE 'ConfigureOpenTelemetry|AddOpenTelemetryExporters|UseOtlpExporter|AddFileLogging' -- 'backend/*.cs'`

Do: in `Orchestration/Extensions/ServiceDefaultsExtensions.cs` delete `ConfigureOpenTelemetry`, `AddOpenTelemetryExporters`
and the static `AddFileLogging(builder, serviceName)`; `AddServiceDefaults` calls `builder.AddTelemetryDefaults()`
(`using Infrastructure.Telemetry;`) where it called `builder.ConfigureOpenTelemetry()`. The service name, the instance id and
the OTLP endpoints are read from the configuration of the host, not from `Environment`; the per-signal
`OTEL_EXPORTER_OTLP_<SIGNAL>_ENDPOINT` are honoured (`backend/Infrastructure/CLAUDE.md`). A meter or a trace source of the
game is registered by `AddBackendMeter` / `AddTraceSource`, not by `AddMeter` / `AddSource` on a provider (peace-data-reborn:
`.AddMeter("Backend")` and the loop over `TraceExtensions.AllSources` go with the deleted method). Tests
(`backend/Tools/Tests/Telemetry/FileLoggerProviderTests.cs` of pixel-swarm and peace-data-reborn):
`ServiceDefaultsExtensions.AddFileLogging(builder, "test-service")` -> `builder.AddFileLogging("test-service")`
(`using Infrastructure.Logging;`).

### 2. BREAKING: `ConfigsInitializer.Init(environment, ...)` reads the content root and throws on a missing file

Who: backend, every game (the coordinator); mines-leader also its game configs and the client telemetry.

Search: `git grep -nE 'ConfigsInitializer\.Init' -- 'backend/*.cs'; ls backend/Orchestration/Coordinator/config.*.json`

Do:
- `ConfigsInitializer.Init("config.x", state, logger)` -> `ConfigsInitializer.Init(environment, "config.x", state, logger)`,
  `IHostEnvironment environment` injected into the setup. The overload with a default value is removed: the value lies in
  the file (mines-leader `Cluster.Game/Configs/GameConfigs.cs`: `config.matchMaking`, `config.achievements` drop
  `MatchMakingOptions.CreateDefault()` / `InGameAchievementOptions.CreateDefault()`, their files are there).
- The file is read from `IHostEnvironment.ContentRootPath` (the project folder under Aspire, `/app` in a container), not
  from `AppContext.BaseDirectory`, and a missing file throws in every environment: on a fresh database the coordinator
  of a game without the file fails its setup. Add `config.runtimePipe.json` and `config.runtimeChannel.json` with `{}` to
  `backend/Orchestration/Coordinator` of every game (no game has them; the defaults of the framework were taken silently).
- mines-leader: `backend/Orchestration/Coordinator/config.clientTelemetry.json` with `{ "IsEnabled": true }`, and
  `Orchestration/Extensions/ClientTelemetryDefaultSetup.cs` calls `ConfigsInitializer.Init(environment,
  "config.clientTelemetry", _state, _logger)`.

Check: the cluster starts on an empty database (`control`), the coordinator log has `[ConfigsInitializer] Seeding config`
for every file.

### 3. BREAKING: `MetricsSnapshotService` -> `builder.AddMetricsSnapshots()` (`Cluster.Monitoring`)

Who: backend, every game.

Search: `git grep -nE 'MetricsSnapshotService' -- 'backend/*.cs'`

Do: `builder.Services.AddHostedService<MetricsSnapshotService>()` -> `builder.AddMetricsSnapshots()` (`using
Cluster.Monitoring;`); the service is internal now, the only way to register it. The file is `metrics_<role>.json` by
`IServiceEnvironment.Tag` (was `SERVICE_NAME` or the entry assembly). In a monolith the hosts write none, the process
writes one `metrics_<deploy>.json` (`backend/Cluster/CLAUDE.md`). peace-data-reborn: delete the `MetricsSnapshot_*` tests
of `backend/Tools/Tests/Telemetry/TelemetryPathsTests.cs`, they are the framework's (`backend/Tests/Telemetry`).

### 4. Optional: several hosts in one process (`MonolithRunner`)

Who: backend, a game that wants a monolith deploy.

Do: `backend/Cluster/CLAUDE.md` § Monolith.

### 5. BREAKING: the backend traces of the console have two sources (`AddConsoleAspireDashboardTraces`, `AddConsoleVictoriaTraces`)

Who: backend, a game whose console calls `AddConsoleBackendTraces` (mines-leader) or uses the moved types.

Search: `git grep -nE 'AddConsoleBackendTraces|ConsoleBackendTraceOptions|BackendTraceReader|BackendTraceParser|OtlpSpan|OtlpJsonValues|BackendTraceResult\.NotFound\(' -- 'backend/*.cs' 'backend/*.razor'`

Do:
- The Aspire dashboard: `builder.AddConsoleBackendTraces(section | options)` -> `builder.AddConsoleAspireDashboardTraces(section |
  options)`; `ConsoleBackendTraceOptions` -> `AspireDashboardTraceOptions` (`using Console.Infrastructure.BackendTraces.AspireDashboard;`),
  the same `Url` / `ApiKey` section. `BackendTraceReader` / `BackendTraceParser` -> `AspireDashboardTraceReader` /
  `AspireDashboardTraceParser`, `OtlpJsonValues` moved to the same namespace, `OtlpSpan` -> `BackendTraceWireSpan`,
  `BackendTraceParser.UnknownService` -> `BackendTraceWireSpan.UnknownService`; `BackendTraceResult.NotFound` carries a message.
- A deployed console without the dashboard (the observability stack, `deploy/observability/CLAUDE.md`): call
  `builder.AddConsoleVictoriaTraces(builder.Configuration.GetSection("BackendTraces"))` with `BackendTraces__Url=http://victoriatraces:10428`
  in its compose; the console service joins the network `observability`. One source per console: a second call throws, so the
  local and the deploy composition call one each (`backend/Console/CLAUDE.md` § Backend traces).
