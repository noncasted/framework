# Not released yet

Steps for the games from the commits after the last tag, the format is in [README](README.md). A new step is added at the end in the same
commit as the change. At a release the steps move to `<version>.md`, only this header stays here.

### Optional: the deploy exports to the observability stack of the host, no `aspire-dashboard`

Who: backend deploy, a game with an `aspire-dashboard` service in its compose (mines-leader, peace-data-reborn,
gamejam-autobattler; pixel-swarm did it). Why: the dashboard keeps its telemetry in memory without a limit, 75-84 MiB fresh and
0.9-1.7 GiB per project after weeks on firstvds; the stack (`otlp`, `victorialogs`, `victoriatraces`) costs ~32 MiB per host,
shared by all its games (`backend/measurements/RESULTS.md`). Logs and traces are then read with the skill `backend-logs`.

Search: `git grep -nE 'aspire-dashboard|OTEL_EXPORTER_OTLP_HEADERS|OTEL_API_KEY|AspireDashboard__|ExportMetrics|otlp:4318' -- 'backend/Tools/deploy/*.yaml'`
(the dashboard is left while `aspire-dashboard` or `OTEL_API_KEY` is listed; done when only `otlp:4318` and `ExportMetrics` are).

Do (the contract: `deploy/observability/CLAUDE.md`; the example: pixel-swarm `backend/Tools/deploy/docker-compose.yaml`,
`docker-compose.local.yaml`, `COOLIFY.md` § Telemetry):
- Delete the service `aspire-dashboard` and its volume (`aspire-dashboard-keys`) from the compose and from
  `docker-compose.local.yaml`; locally the network `observability` must exist (`docker network create observability`, the stack
  itself is optional: `deploy/observability/README.md`).
- Declare `observability: external: true` next to the own network; every exporting .NET service joins both, the own one first by
  priority (otherwise a short name like `silo` resolves to another game of the host):
  `networks: { <game>: { gw_priority: 100 }, observability: {} }`.
- The env of the .NET services: `OTEL_EXPORTER_OTLP_ENDPOINT=http://otlp:4318`, `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` (no gRPC
  port, VictoriaLogs rejects JSON), `OTEL_RESOURCE_ATTRIBUTES=service.namespace=<key of deploy/registry.json>,deployment.environment=production`,
  `Telemetry__ExportMetrics=false` (the stack stores no metrics); delete `OTEL_EXPORTER_OTLP_HEADERS` and the API key.
- The console: `AspireDashboard__*` -> `BackendTraces__Url=http://victoriatraces:10428` and `AddConsoleVictoriaTraces` in the deploy
  composition (step 9 of [0.17.0](0.17.0.md)).
- Coolify: delete the variables of the dashboard (`OTEL_API_KEY`, `ASPIRE_API_KEY`, ...) and its domain; `deploy/registry.json` of
  the framework: drop `domains.aspire` of the project (`deploy/CLAUDE.md` § Rules).

Check: `tools/framework/tools/scripts/deploy/prod tail --project <project>` shows the logs of every role after the deploy; `docker exec <meta container>
getent ahostsv4 silo` prints one address.

### Optional: the backend as one process (`MonolithRunner`), the deploy recipe

Who: backend deploy, a game that wants a monolith deploy (pixel-swarm runs one on ovh). Why (`backend/measurements/RESULTS.md`):
pixel-swarm prod, idle, cost (anon + swap): five services + `aspire-dashboard` 666 MiB -> monolith on the observability stack
140 MiB (.NET 542 -> 87); local Release, 5 minutes idle: five services 315-342 MiB -> monolith 141-142 (server GC 178).

Search: `git grep -nE 'MonolithRunner|static WebApplication Build\(WebApplicationOptions|SetApplicationName|target: monolith' -- 'backend/*.cs' 'backend/*.yaml'`

Do (the contract: `backend/Cluster/CLAUDE.md` § Monolith; the example: pixel-swarm `backend/Orchestration/Monolith/Program.cs`,
`backend/Orchestration/<Role>/<Role>Host.cs`, `backend/Orchestration/Dockerfile`, `backend/Tools/deploy/docker-compose.monolith.yaml`,
`COOLIFY.md` § Monolith):
- A host factory per role, `public static WebApplication Build(WebApplicationOptions options)` (`SiloHost`, `CoordinatorHost`,
  `MetaHost`, `GameHost`, `ConsoleHost`), called by the `Program.cs` of the separate service and by the monolith alike.
- A project `Orchestration/Monolith` (an exe referencing every role) whose `Program.cs` calls `MonolithRunner.Create(args, [...]).Run(...)`
  with one `MonolithHost` per role; `ApplicationName` is the library of the role, the exe itself for the console (its static web assets).
- A Dockerfile target `monolith` (its own publish of `Monolith.csproj`, `ENTRYPOINT ["dotnet", "Monolith.dll"]`); the services keep the
  default target.
- A compose with one service `backend`: `Monolith__Hosts__<Tag>__Port` per role, `services__game__http__0` /
  `services__meta__http__0` = `http://localhost:<port>` for the console, `SERVICE_NAME` = the deploy name, `DOTNET_gcServer=0`, a
  healthcheck over `/ready` of every port, `stop_grace_period: 30s`, the telemetry of the step above.
- Traefik labels of meta / game / console in the compose (routers http + https per role, `loadbalancer.server.port`) with empty Domains
  of `backend` in Coolify: Coolify keeps one port per service and drops `:port` of the Domains.
- The console sets `AddDataProtection().SetApplicationName("<game>-console")` and mounts the keys volume of the services compose,
  so the cookies survive a switch between the modes (the first deploy with the name logs the users out once).
- `deploy/registry.json`: `mode: monolith`, `compose.services` / `compose.monolith`, the deploy branch.
- A role split into a library + an exe (`Orchestration/<Role>` + `Orchestration/Services/<Role>`): the `config.*.json` of the
  coordinator move with the exe into `Services/Coordinator`, its content root under Aspire (`ConfigsInitializer` reads the content
  root, step 6 of [0.17.0](0.17.0.md)); a `Content` item with `CopyToPublishDirectory` there for the container (`/app`), the
  monolith links them into `hosts/Coordinator`, the `CoordinatorConfigFolder` of the console in Development points at it.

### The `/ready` path from the framework, no health spans

Who: backend, every game that maps `/ready` (pixel-swarm `Orchestration/Extensions/ServiceDefaultsExtensions.cs`). Why: the
default instrumentation now drops the server spans of `/ready` next to `/health` and `/alive`; the healthchecks of the composes
poll it every 2-3 s per port, 150-220 thousand spans a day per game in the trace store of the host.

Search: `git grep -nE '"/(ready|health|alive)"' -- 'backend/*.cs'`

Do: map `TelemetryInstrumentation.ReadyEndpointPath` (`HealthEndpointPath`, `AlivenessEndpointPath`) instead of a local constant
or literal; a check of the health paths (an auth middleware skipping them) calls `TelemetryInstrumentation.IsHealthCheck(path)`.

Check: a few minutes after the deploy VictoriaTraces of the host has no new span `GET /ready` of the project (LogsQL
`name:"GET /ready"` over `/select/logsql/query`, through the ssh tunnel of `backend-logs`).

### The analyzer dll in the backend images and the deploy mirror

Who: backend deploy, every game that did step 3 of [0.17.0](0.17.0.md) and whose image context or deploy branch leaves out
`tools/framework/client` (a `.dockerignore` entry, a backend-only mirror like pixel-swarm `tools/scripts/sync-deploy-branch.sh`).
Why: `backend/Directory.Build.props` compiles with `tools/framework/client/core/Analyzers/Framework.Analyzers.dll`; without the
file every project fails with `CS0006: Metadata file ... Framework.Analyzers.dll could not be found` (pixel-swarm: the Coolify
build of `deploy-monolith` failed in `dotnet publish` of the migrator).

Search: `git grep -nE 'tools/framework/client|FRAMEWORK_PATHS|sparse-checkout' -- .dockerignore 'tools/scripts/*.sh' '.github/workflows/*.yml'`

Do: after `tools/framework/client/` of `.dockerignore` add `!tools/framework/client/core/Analyzers/Framework.Analyzers.dll`; a
deploy mirror copies that file too, keeping its path (pixel-swarm: `FRAMEWORK_PATHS` of `sync-deploy-branch.sh`, the
sparse checkout and the `cp --parents` of `.github/workflows/sync-deploy-branch.yml`).

Check: `dotnet publish` of each backend exe from a checkout of the deploy branch (or `docker compose build`) has no `CS0006`.
