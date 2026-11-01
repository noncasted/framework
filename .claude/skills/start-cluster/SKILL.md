---
name: start-cluster
description: Start the backend Aspire cluster. Use when any task requires a running cluster — benchmarks, tests, API calls, telemetry analysis. Also trigger on "start cluster", "start backend", "start server", "run cluster".
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /start-cluster, start-cluster, запусти кластер, start cluster, start backend, start server, run cluster, запусти бэкенд, запусти сервер
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Start Cluster

Start the Aspire backend cluster and wait for it to be ready.

## Launch Command

```bash
dotnet run --project backend/Orchestration/Aspire/Aspire.csproj --launch-profile http
```

IMPORTANT: `aspire run` does NOT support `--launch-profile` (known issue). Always use `dotnet run` instead.

The `http` profile is the first entry in `backend/Orchestration/Aspire/Properties/launchSettings.json` (dashboard on `http://localhost:7100`). All service-to-service transport is plain HTTP — no pfx/dev-cert is needed. The AppHost is dev-only; production (Coolify) runs docker compose, see `.claude/docs/DEPLOY.md`.

## Procedure

1. Check if cluster is already running:
```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:7103/api/benchmarks 2>/dev/null
```
If returns `200` — cluster is already running, skip startup. `503` means the console is up but the cluster is still initializing (`ClusterReadyFilter`) — just poll.

2. Start in background:
```bash
dotnet run --project backend/Orchestration/Aspire/Aspire.csproj --launch-profile http 2>&1 &
```
Use `run_in_background=true` for the Bash tool.

3. Poll until API is ready (up to 2 minutes):
```bash
for i in $(seq 1 24); do
  code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:7103/api/benchmarks 2>/dev/null)
  if [ "$code" = "200" ]; then echo "READY"; break; fi
  sleep 5
done
```

4. If cluster fails to start, check the background process output and report the error to the user.

## Stopping the Cluster

```bash
pkill -f "Aspire.AppHost"     # the AppHost process (bin/Debug/net10.0/Aspire.AppHost); dcp stops the services with it
```

## Key Ports

| Service | Port |
|---------|------|
| ConsoleGateway (API + UI) | 7103 |
| GameGateway | 7102 |
| MetaGateway | 7101 |
| Coordinator | 6001 |
| Silo (HTTP health endpoints) | 6002 |
| Aspire Dashboard | 7100 |
| PostgreSQL (container `postgres`) | 9432 |
| PgBouncer | 9433 |
| Orleans silo / gateway (localhost clustering) | 11111 / 30010 |

Service ports come from each project's `Properties/launchSettings.json` under `backend/Orchestration/<Service>/`;
Postgres from the `db` connection string in `backend/Orchestration/Aspire/appsettings.json`.

## Requirements

- Docker must be available: the AppHost starts the `postgres` (the volume is named after the project) and `pgbouncer` containers itself
- Migrations (`backend/Tools/DeploySetup`, `PostResourcesSetup.Run`) run automatically after the resources are created
