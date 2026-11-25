---
name: backend-logs
description: Find where a game is deployed (server, Coolify uuids and MCP server, domains, ssh alias) and read the production logs and traces of its backend from the VictoriaLogs / VictoriaTraces stack of its VPS over an ssh tunnel - errors and warnings grouped by message template, LogsQL queries, the span tree of a trace, the logs and spans of one trace, the last lines - and check the deploy registry against both Coolify panels. Use when the user asks what failed after a deploy, why a match did not start, for the backend or production logs, a trace, the backend side of a client error, where a project is deployed, or to verify the registry ("логи прода", "логи бэкенда", "посмотри логи бэкенда", "что падало после деплоя", "ошибки на проде", "почему матч не стартовал", "трейс", "покажи трейс", "логи по трейсу", "где задеплоен проект", "на каком сервере", "сверь реестр").
---

# Backend logs

One read-only script: `tools/scripts/deploy/prod` of the framework (`tools/framework/tools/scripts/deploy/prod` from a game,
`/projects/framework/tools/scripts/deploy/prod` from anywhere else). The usage and the exit codes are in the comment at its top
and in `common.py`; the stack, its fields and LogsQL examples are in `deploy/observability/CLAUDE.md`, the registry in
`deploy/CLAUDE.md` (of the framework).

- The project is the repository of the current directory (by its origin, so a worktree of a game works), or `--project <key>`
  of `deploy/registry.json`. Every query is limited to its `service.namespace` by the store, not by the query text.
- The registry is read from `origin/main` of the framework (fetched at most every 5 minutes), not from the submodule.
- The stores are read through `ssh -N -L` to the alias of the server, opened for one command and closed after it. No tokens.
  An ssh, network or query error is printed with a non-zero exit: report it, do not retry blindly.
- Times are UTC. A log is searchable seconds after it is written, a span 20-30 s after the end of its trace.
  Logs live 30 days, spans 14.

## Commands

| Command | What |
|---|---|
| `prod registry show [<project>]` | server, mode, domains, Coolify project and resources, MCP server, ssh alias; outside a game a table of all |
| `prod registry verify` | the registry against both Coolify panels (tokens of `~/.claude/mcp/coolify.json`); exit 5 with the differences |
| `prod errors [--since 1h] [--service meta]` | warnings and errors by message template (numbers collapsed to `<N>`): count, last time, an example, its trace id |
| `prod logs --query '<LogsQL>' [--since 1h] [--service S] [--limit 200]` | any LogsQL (filters, `| stats`, `| fields`); log rows as lines, other rows as `key=value` |
| `prod trace <trace id> [--since 1d]` | the span tree: offset, duration, service, name; a failed span with its attributes and exception |
| `prod around <trace id> [--since 1d]` | the logs and spans of the trace on one time line |
| `prod tail [--since 5m] [--service S] [--limit 50] [--follow]` | the last lines; `--follow` polls until interrupted (run it in the background) |

`--service` is the role (`meta`, `game`, `silo`, `coordinator`, `console`). Test aids: `--namespace smoke-test` reads another
namespace on the server of the project (the smoke records of `deploy/observability/smoke.py`), `--logs-url` / `--traces-url`
read a local stack without the tunnel.

## Scenarios

- **What failed after the deploy.** `prod errors --since <time since the deploy>`; for a template that matters,
  `prod around <its trace id>` or `prod logs --query '"<words of the message>"'`. The deploy time: MCP `list_deployments`
  of the server (`registry show` names the MCP server and the uuid of the backend).
- **Why the match did not start.** `prod errors --since 30m --service game` and `--service meta`; then the trace of the failed
  request: `prod trace <id>` shows which span failed and why, `prod around <id>` what was logged next to it.
- **Logs around a client error.** The startup record of the client telemetry holds the backend trace of the authorization
  of its socket (skill `client-telemetry`: `backend_trace=` of `telemetry startup <id>`): `prod around <that id> --since 3d`.
- **Where is project X deployed.** `prod registry show X`; the domains and uuids come from the registry, the state of the
  resources from the MCP server it names.
