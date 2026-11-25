# deploy/observability

The logs and traces of every game on a VPS: one stack per host (`ovh`, `firstvds`), deployed by the Coolify of that host
from the branch the framework publishes. `docker-compose.yaml` is the stack, `README.md` how to run and install it. This file
is the contract with the games and the way an agent reads a project. `smoke.py` sends one log and one span and finds
them (README.md).

| Service | Image | Inside the network `observability` | On the host |
|---|---|---|---|
| `otlp` | `nginx:1.30.8-alpine-slim` | `http://otlp:4318` OTLP/HTTP router | - |
| `victorialogs` | `victoriametrics/victoria-logs:v1.54.0` | `http://victorialogs:9428` | `127.0.0.1:9428` (read) |
| `victoriatraces` | `victoriametrics/victoria-traces:v0.13.0` | `http://victoriatraces:10428` | `127.0.0.1:10428` (read) |

Retention: logs 30 days or 4 GiB, traces 14 days or 4 GiB, whichever comes first. Memory limits 256 / 256 / 32 MiB.
Metrics are not stored.

## Contract for the games

Games on different framework versions export into the same stack, so everything below only changes compatibly: names,
ports, paths and stream fields stay; a new thing is added next to the old one; a removal waits until no deployed game
uses it (check every project of `../registry.json`). A breaking change of the stack is a framework release step for all games.

- **Network.** Every service that exports joins the external Docker network `observability` (created once per host,
  `README.md`). Nothing is published for ingest: it is reachable only from that network.
- **Own network first.** The services of every game on the host share `observability`, so a short name (`silo`,
  `pgbouncer`, `coordinator`) resolves there too, and Docker searches the networks of a container in name order: with
  `observability` before the own network (`pixel-swarm`, `peace-data`, a Coolify uuid) `silo` returns the silos of several
  games (checked on Docker 29). A game therefore gives its own network a higher `gw_priority` (Docker resolves names from it
  first; checked):
  ```yaml
  networks:
    pixel-swarm:
      gw_priority: 100
    observability: {}
  ```
  Check on the host: `docker exec <meta container> getent ahostsv4 silo` prints the addresses of one container.
- **Export.** OTLP/HTTP with protobuf; there is no gRPC port.
  ```
  OTEL_EXPORTER_OTLP_ENDPOINT=http://otlp:4318
  OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
  ```
  The router takes `/v1/logs` (to VictoriaLogs, with the stream fields below), `/v1/traces` (to VictoriaTraces) and
  `/v1/metrics` (answers 200 with an empty body and drops it, so an exporter left on neither retries nor logs errors; a game
  still turns metrics export off in its deploy). Any other path is 404.
- **Without the router** (allowed, the same contract): per-signal endpoints straight to the stores.
  ```
  OTEL_EXPORTER_OTLP_LOGS_ENDPOINT=http://victorialogs:9428/insert/opentelemetry/v1/logs?_stream_fields=service.namespace,service.name,deployment.environment
  OTEL_EXPORTER_OTLP_TRACES_ENDPOINT=http://victoriatraces:10428/insert/opentelemetry/v1/traces
  ```
  The `_stream_fields` query argument is required here: without it VictoriaLogs makes every resource attribute a stream
  field, and `service.instance.id` starts a new stream on each restart. VictoriaLogs rejects OTLP JSON
  (`json encoding isn't supported for opentelemetry format`); VictoriaTraces takes both.
- **Resource attributes.**
  - `service.namespace` = the project key of `../registry.json` (`pixel-swarm`): separates the games on the host.
  - `service.name` = the role (`silo`, `coordinator`, `meta`, `game`, `console`; one name per process in the monolith).
  - `deployment.environment` = `production`.

  Usually `OTEL_RESOURCE_ATTRIBUTES=service.namespace=<project>,deployment.environment=production` in the compose of the
  game and `service.name` from the role. These three are the stream fields of the logs.
- **Field names** are the ones VictoriaLogs v1.54 gives OTLP data (below). `-opentelemetry.consistentPrefix` of later
  versions renames them (`resource.attribute.*`): it stays off, turning it on is a breaking change of the queries.

## Where a project lives

Only `../registry.json` (read from the latest main, see `../CLAUDE.md`): `projects.<project>.server` names the server and
`projects.<project>.namespace` the `service.namespace`; `servers.<server>.ssh` is the ssh alias and
`servers.<server>.observability.logs` / `.traces` the read addresses on the host (`access: ssh-tunnel`). A project has no
files of its own about it. The project is the name of the repository root, or given explicitly.

## Reading: ssh tunnel

The read ports are bound to `127.0.0.1` of the host; an agent forwards them with the ssh alias, no tokens. Local ports are
free choices (here 19428 / 20428, another server takes others):

```bash
ssh -M -S /tmp/obs-ovh.sock -fN -o ExitOnForwardFailure=yes \
    -L 19428:127.0.0.1:9428 -L 20428:127.0.0.1:10428 ovh
# ... queries on http://127.0.0.1:19428 (logs) and http://127.0.0.1:20428 (traces) ...
ssh -S /tmp/obs-ovh.sock -O exit ovh
```

The insert paths are reachable through the tunnel too: an agent only reads (`smoke.py` is the one writer, into
`service.namespace=smoke-test`). A log is searchable a few seconds after ingest, a span by LogsQL as fast, a trace by the
Jaeger API 20-30 s after its last span (`-insert.indexFlushInterval`, 20 s). A query always filters by `service.namespace`: the stack holds every game of the host.

## Logs: LogsQL

`POST /select/logsql/query` with `query=`, answers JSON lines; optional `limit=`, `start=` / `end=`.
Fields: `_time`, `_msg` (the body), `_stream`, `service.namespace`, `service.name`, `deployment.environment`,
`service.instance.id`, `severity_text` (`Information`, `Warning`, `Error`, ...), `severity_number`, `trace_id`, `span_id`,
`scope.name` (the logger category), the attributes of the record by name. A name with a dot is quoted in a filter.

```bash
L=http://127.0.0.1:19428
# the last hour of a project, newest last
curl -s $L/select/logsql/query --data-urlencode 'query=_time:1h {service.namespace="pixel-swarm"} | sort by (_time) | limit 200'
# errors of one role
curl -s $L/select/logsql/query --data-urlencode 'query=_time:1d {service.namespace="pixel-swarm", service.name="meta"} severity_text:in(Error, Critical)'
# the logs of one trace
curl -s $L/select/logsql/query --data-urlencode 'query=_time:1d {service.namespace="pixel-swarm"} trace_id:="<32 hex>" | fields _time, service.name, severity_text, _msg'
# a word in the message, counts per role
curl -s $L/select/logsql/query --data-urlencode 'query=_time:1d {service.namespace="pixel-swarm"} "timeout" | stats by (service.name) count() n'
# the streams (games and roles) of the host
curl -s $L/select/logsql/streams --data-urlencode 'query=_time:1d'
```

## Traces

Jaeger query API under `/select/jaeger/api/` (`start` / `end` in microseconds, `limit` default 20). Services are role names
shared by all games, so a search filters the namespace by the tag `resource_attr:service.namespace`; span attributes are
tags by their own name.

```bash
T=http://127.0.0.1:20428
NOW=$(date +%s); S=$(( (NOW-3600)*1000000 )); E=$(( NOW*1000000 ))
curl -s $T/select/jaeger/api/services
curl -s $T/select/jaeger/api/services/meta/operations
curl -s -G $T/select/jaeger/api/traces --data-urlencode service=meta \
  --data-urlencode 'tags={"resource_attr:service.namespace":"pixel-swarm"}' \
  --data-urlencode start=$S --data-urlencode end=$E --data-urlencode minDuration=100ms --data-urlencode limit=10
curl -s $T/select/jaeger/api/traces/<32 hex trace id>
```

The spans are also LogsQL rows on the same port (`/select/logsql/query`): `name`, `trace_id`, `span_id`, `parent_span_id`,
`duration` (ns), `kind`, `status_code`, `scope_name`, `resource_attr:<name>`, `span_attr:<name>`. Rows with
`trace_id_idx` instead of `trace_id` are the index of VictoriaTraces; a filter on `name:*` skips them.

```bash
curl -s $T/select/logsql/query --data-urlencode 'query=_time:1h "resource_attr:service.namespace":="pixel-swarm" name:* | stats by (name) count() n, quantile(0.95, duration) p95_ns | sort by (p95_ns desc) | limit 20'
```

The web UI of each store is `/select/vmui` (through the tunnel).
