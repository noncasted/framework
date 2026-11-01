---
name: start-cluster
description: Start the backend Aspire cluster. Use when any task requires a running cluster — benchmarks, tests, API calls, telemetry analysis. Also trigger on "start cluster", "start backend", "start server", "run cluster".
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /start-cluster, start-cluster, запусти кластер, start cluster, start backend, start server, run cluster, запусти бэкенд, запусти сервер
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Start Cluster

Start the Aspire backend cluster and wait for it to be ready.

## Procedure

```bash
.claude/skills/control/control cluster start
```

It returns when the cluster answers, and does nothing when it is already up. Also `status`, `stop`, `restart`, `rebuild`, `logs`: see the `control` skill.
Do not ask the user first. `aspire run` does not support `--launch-profile`, so the command uses `dotnet run`. The AppHost is dev-only; production
(Coolify) runs docker compose, see `.claude/docs/DEPLOY.md`.

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
