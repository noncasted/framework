# Match Metrics: the Game Data of a Match

**TL;DR:** the module `match-metrics` keeps what a game writes about a match: series (counters and gauges with tags) in
buckets of one second of the clock of the run, and rare events. A client writes into a stream it opens per run; batches go
over the meta socket every 30 s, one in flight, kept and sent again after a reconnect. The backend checks the writer
with the game, stores the batches in its own Postgres tables and reads them back in the process or over HTTP.

The rules of the wire, the limits and the routes are in `backend/MatchMetrics/Contract/CONTRACT.md`: this doc does not
repeat them. It is the map of the parts and the wiring of a game.

---

## Parts

| Part | Where | Assembly / namespace |
|------|-------|----------------------|
| Client writer and sender | `client/modules/match-metrics` (package `com.noncasted.framework.match-metrics`) | `Internal.MatchMetrics` / `Global.MatchMetrics` |
| Messages | `shared/MatchMetrics/SharedMatchMetrics.cs` | `Framework.Shared` / `Shared` |
| Intake, storage, server stream, reading | `backend/MatchMetrics` (`MatchMetrics.csproj`) | `Framework.MatchMetrics` / `Game.MatchMetrics` |
| Contract | `backend/MatchMetrics/Contract/CONTRACT.md` | |
| Tests | `client/modules/match-metrics/Tests/Editor` (`Internal.MatchMetrics.Tests`), `backend/Tests/MatchMetrics` (Postgres of the test fixture) | |

Not the `telemetry` module. Telemetry is anonymous, off by a console flag, best effort, kept 7 days. Match metrics are
bound to a player and a match, always on, at least once with dedup, kept in tables for queries across matches. The
modules do not reference each other. "metrics" alone is the OpenTelemetry of the backend (`backend/.telemetry/metrics`).

---

## Model

- **Stream** - one writer in one match: `MatchId` (the id of the session of the match), the user (the meta id, taken from
  the socket, never sent by the client), `WriterId` (a new guid per stream), `Kind` (`Client` / `Server`). A second `Open`
  of the same writer after a reconnect finds the same stream and its last `Seq`.
- **Series** - a name and up to 4 tags `key=value` (`MatchTags`). The kind is `Counter` (the sum of a bucket) or `Gauge` (the
  last value of a bucket; nothing set - unchanged). Ids (an upgrade, a source, a kind of enemy) are tags, not parts of the
  name. The same name and tags in any order are one series of the game. Up to 256 series per stream.
- **Bucket** - one second of the clock of the run (`IMatchMetricsClock.Seconds`, given by the game). A **chunk** is a run of
  buckets of one series whose real time goes with the clock. Where the real time and the clock part by more than a second
  (a pause, a guest that jumps to the clock of the host) the chunk ends. A stopped clock makes no buckets: no hole.
- **Event** - a name, the time of the clock, up to 8 fields (`MatchEventField`: a number or a short code text). For rare
  things: a pick, a death, a pause.
- **Names** - `[a-z0-9._]` up to 64; tag keys up to 32; tag values and texts `[a-z0-9._-]` up to 32. No free text of a player.
- **Two axes.** The clock of the run is the axis of the series and the events (one for all players of a match). The server
  time `at` of a chunk or an event is its receive time minus its age (`AgeMs`): no client clock is trusted.

---

## Client

API (`Global.MatchMetrics`):
- `IMatchMetrics.Open(lifetime, matchId, IMatchMetricsClock)` -> `IMatchMetricsStream`. The end of the lifetime cuts the
  last batch; the sending of what is left goes on after it, while the scope of `IMatchMetrics` lives.
- `IMatchMetricsStream.Counter(name, tags)` -> `IMatchCounter.Add(value)`; `Gauge(name, tags)` -> `IMatchGauge.Set(value)`;
  `Event(name, params MatchEventField[])`.
- `MatchTags.Of("source", "orb").With("card", "12")` - a value type; `MatchEventField.Of(key, number | text)`.
- `MatchMetricsOptions(sendIntervalSeconds = 30, tickSeconds = 1, bufferedBatches = 20)`.

How a game writes:
- Declare each handle once (a setup, the first time a tag value appears) and keep it. A write through a handle allocates
  nothing and may run every frame. The same name and tags give the same handle.
- A wrong name, a series over the limit, `NaN` in a counter, a write after the end of the stream throw (fail fast).
- One class per feature that writes (P1): it takes the stream from the game, subscribes to its event and writes.

Sending (`Sending/MatchMetricsSender`, `Writing/MatchMetricsBuffer`):
- `MatchMetrics` ticks every `Tick` of real time: it moves the buckets on the clock, cuts a batch every `SendIntervalMs`
  and pumps the senders.
- One request in flight per stream, the head of the buffer first; `Seq` from 1. A new link of the socket opens the stream
  again with the same `WriterId` and drops the batches the server already has (`LastSeq`).
- No answer (a lost socket, a server error) - the batch stays and goes again with its `Seq`. `Closed`, `NotInMatch`,
  `Rejected` stop the sending of this stream with a log.
- Over `BufferedBatches` (20 x 30 s - ten minutes without the server) the oldest unnumbered batch is dropped with an error.

Transport: `AddSocketMatchMetrics()` registers `MatchMetricsSocketTransport` over `IMatchMetricsSocket.Connection` (the
meta socket of the game). `AddMatchMetrics()` alone needs an `IMatchMetricsTransport` of the game (tests use a fake).

---

## Protocol

`SharedMatchMetrics.Open` -> `Opened`, `Batch` -> `BatchResult`; union tags `NetworkContextTags.MatchMetricsOpen` 220,
`MatchMetricsOpened` 221, `MatchMetricsBatch` 222, `MatchMetricsBatchResult` 223 (the range 220-229 of the framework, comment
in `shared/Protocol/INetworkContext.cs`). Series tags on the wire are `TagKeys[]` / `TagValues[]` of `MatchSeriesDecl`. A
series is declared in the first batch that uses it.

---

## Backend

Intake (the gateway of the meta socket):
- `IMatchMetricsConnections.Of(session.Lifetime, session.UserId)` -> `MatchMetricsConnection` with `Open` and `Accept`. The
  game keeps its own socket commands and calls these; the user always comes from the session.
- `IMatchMetricsAdmission.Check(matchId, userId)` -> `MatchMetricsAdmit(IsPlayer, ClosedAt)` - the answer of the game (the
  collector knows nothing of matches). Asked on `Open` and again after `AdmissionRecheck` (60 s). A closed match still takes
  batches during `CloseGrace` (2 min): the last batch of a run leaves when the match closes.
- `MatchMetricsBatchParser` checks every batch against `MatchMetricsLimits` (the client is not trusted).

Storage (`Storage/`, Npgsql over `IDbSource`, not grain states):
- `MatchMetricsSchema` - 6 tables: `match_metric_series` (dictionary), `match_metric_streams`, `match_metric_slots` (the slot
  map of a stream, for a reconnect to another gateway), `match_metric_chunks` (`values real[]`), `match_metric_totals` (sum,
  count, max per stream and series), `match_metric_events` (`fields jsonb`). `MatchMetricsSchema.Create` makes the missing
  ones.
- `MatchMetricBatchWriter` - one transaction per batch: `last_seq` moves from `Seq - 1` only (a copy - `Duplicate`, a gap -
  `Rejected`), `COPY` of chunks and events, upsert of totals. A failure of the database is an error answer: the client repeats.
- Volume at 1 s buckets: ~0.8 MB per match of 4 players and 10 min. Nothing is cleaned.

Server stream: `IMatchMetricsWriter.Open(lifetime, matchId)` -> `IMatchMetricsServerStream` (`Event`, `Gauge`). Kind
`Server`, user `Guid.Empty`, the axis is the server time since the open (the server has no clock of the run). Flushed every
`ServerFlush` (30 s) and at the end of the lifetime. Only what the clients cannot see (the host of a session, a lost seat).

Reading:
- In the process: `IMatchMetricsReader` (`GetStreams`, `GetSeries`, `GetPoints`, `GetTotals`, `GetEvents`) - for a console,
  the history of matches, agents. A bad query throws `MatchMetricsQueryException`.
- Over HTTP: `MapMatchMetricsRead()` - GET `/api/match-metrics/series`, `/totals` (across matches),
  `/matches/{matchId}/streams|series|totals|events`, with `groupBy`, `tag.<key>`, `step` 1/10/60, `axis` run/server. Always
  under the console token of the game: `MapMatchMetricsRead().RequireConsoleToken()`.

Registration (`MatchMetricsExtensions`, on `IHostApplicationBuilder`):

| Host | Calls |
|------|-------|
| Gateway of the meta socket | `AddMatchMetrics()`, `AddMatchMetricsAdmission<T>()`, the game commands over `IMatchMetricsConnections` |
| Host that reads | `AddMatchMetricsReader()`; endpoints `MapMatchMetricsRead().RequireConsoleToken()` |
| Game server | `AddMatchMetricsWriter()` |
| Deploy setup | `await MatchMetricsSchema.Create(connection)` |

---

## Wiring in a game

1. Client: `client/Packages/manifest.json` - `"com.noncasted.framework.match-metrics": "file:../../tools/framework/client/modules/match-metrics"`;
   the asmdef that writes references `Internal.MatchMetrics`.
2. Protocol: four lines `SharedMatchMetrics.Open/Opened/Batch/BatchResult` in the union of the game, then the golden files
   and the protocol hash.
3. Meta scope: the meta backend is `IMatchMetricsSocket`; `AddSocketMatchMetrics()` in that scope. The offline meta answers
   `Open` with `Opened { Ok }` and `Batch` with `BatchResult { Ok }` and stores nothing (no `IsOffline` branch in a writer).
4. Run scope: a clock shared by all players of a match (`IMatchMetricsClock`), a stream opened with the id of the session
   of the match, writers per feature.
5. Backend: `ProjectReference` on `tools/framework/backend/MatchMetrics/MatchMetrics.csproj` in the hosts and the deploy
   setup; the registrations above; an `IMatchMetricsAdmission` over the record of the match.

What one game writes and where it looks: the telemetry doc of the game (`TELEMETRY.md`, "Match metrics").

---

## Traps

- The clock must be one for every player. A local `Time.time` or a simulation time that starts at 0 on each client puts a
  late guest and a new host on another axis.
- Never read the user from a message: the server takes it from the socket. A client opens only `Kind = Client`.
- Declaring a series per hit or per frame allocates and hits the 256 limit: declare once, keep the handle.
- A writer that sums its counters at the end must flush before the stream lifetime ends: a write after the end throws.
