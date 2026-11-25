# Observability stack

VictoriaLogs and VictoriaTraces with a small OTLP/HTTP router (nginx), one stack per VPS for all its games. The games export
OTLP into the Docker network `observability`; agents read over an ssh tunnel. The contract with the games, the access
scheme and the queries are in [CLAUDE.md](CLAUDE.md).

| File | What |
|---|---|
| `docker-compose.yaml` | the stack; deployed as is by Coolify |
| `CLAUDE.md` | the contract (network, addresses, resource attributes), where a project lives, how to read it |
| `smoke.py` | sends one OTLP protobuf log and one span, finds them by LogsQL and the Jaeger API (python stdlib) |

Idle memory (2026-11-25, local, 24 cores): victorialogs 12-22 MiB, victoriatraces 12-32 MiB, otlp 3 MiB; limits
256 / 256 / 32 MiB. Disk: at most 4 GiB of logs (30 days) and 4 GiB of traces (14 days).

## Run locally

```bash
cd deploy/observability
docker network create observability          # once; the stack never creates it
docker compose -p obs-test up -d --wait
./smoke.py --logs http://127.0.0.1:9428 --traces http://127.0.0.1:10428          # straight to the stores
docker run --rm --network observability -v "$PWD":/w:ro python:3.13-alpine \
  python -I /w/smoke.py --logs http://victorialogs:9428 --traces http://victoriatraces:10428 --ingest http://otlp:4318
docker stats --no-stream obs-test-victorialogs-1 obs-test-victoriatraces-1 obs-test-otlp-1
docker compose -p obs-test down -v && docker network rm observability
```

A local game exports into it the same way as on a VPS: its services join `observability` with
`OTEL_EXPORTER_OTLP_ENDPOINT=http://otlp:4318` and `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`. The local Aspire of a game
does not use this stack.

## Install on a new VPS

1. **Network**, once, on the host (ssh alias from `../registry.json`):
   `ssh <alias> 'sudo -n docker network create observability'` (`docker` without sudo where the user is root).
   It is not a Coolify Destination: Coolify only attaches containers to it because the compose names it as external.
2. **Coolify resource.** A new resource of the type Application from the private repository of the framework, branch of
   the stack (published by the GitHub Action of the framework), build pack Docker Compose, compose location
   `/docker-compose.yaml` (the branch holds this folder as its root). No domains: nothing of the stack goes through the
   proxy. No environment variables. The webhook of the repository redeploys it on push. (The details of the Coolify
   setup: step [8] of the task fills them in.)
3. **Registry.** `servers.<server>.observability` of `../registry.json` holds the read addresses
   (`http://127.0.0.1:9428`, `http://127.0.0.1:10428`, `access: ssh-tunnel`).
4. **Check** through the tunnel (CLAUDE.md, "Reading"):
   `./smoke.py --logs http://127.0.0.1:19428 --traces http://127.0.0.1:20428`, and on the host
   `ss -ltn | grep -E ':(9428|10428) '` lists only `127.0.0.1`.

## How Coolify treats this file (Coolify 4.7.6)

- The compose project name is the resource uuid, containers are `<service>-<uuid>`; the service names (`otlp`,
  `victorialogs`, `victoriatraces`) stay as DNS names on every network of the service, which is what the games resolve.
- External networks are kept as declared; Coolify adds its own network `<uuid>` to every service next to them.
- Named volumes become `<uuid>_<name>` (`<uuid>_victorialogs-data`); the uuid does not change, data survives redeploys. Deleting
  the resource may delete them.
- `ports` are passed through as written: `127.0.0.1:9428:9428` stays on the loopback. Never write a port without the address:
  it would publish the store on every interface, Docker bypasses ufw.
- No healthcheck page in Coolify for a compose application: the health of the resource comes from the `healthcheck` of the
  services. The Victoria images have no shell; the binary checks itself with `-healthcheck`.
- A `${VAR:-default}` becomes a Coolify variable that keeps its first value on later deploys, so the settings of the stack
  are literals in the compose: a change in main reaches both hosts.
- Repository files are not mounted on the host for a compose application, so the nginx config is written by the command of
  the `otlp` service.

## Change the stack

- Images: pin the new tags (GitHub releases of `VictoriaMetrics/VictoriaLogs`, `VictoriaMetrics/VictoriaTraces`, the
  stable `nginx:<version>-alpine-slim`), run "Run locally", read the release notes for renamed fields or flags.
- The contract of CLAUDE.md only changes compatibly: games on old framework versions keep exporting into the same stack.
- Retention or limits: edit the literals; both VPS take the same file, so a value must fit the smaller disk (firstvds).
