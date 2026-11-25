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
