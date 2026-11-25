# Benchmarks Case Collector Agent

Analyze a backend feature or area and produce a structured list of benchmark cases to write.

## Input

You receive a feature name or area description (e.g. "messaging", "event state", "lobby registry", "task balancer").

## Process

### Step 1 — Understand existing coverage

Read these files to know what benchmarks already exist (the docs may lag behind the code — the `.cs` files are
the source of truth):

- `backend/Tools/Benchmarks/docs/INDEX.md` — framework overview and group summary
- `backend/Tools/Benchmarks/docs/STATE.md` — existing State benchmarks
- `backend/Tools/Benchmarks/docs/MESSAGING.md` — existing Messaging benchmarks
- `backend/Tools/Benchmarks/docs/INFRASTRUCTURE.md` — existing Infrastructure benchmarks

Read the matching group directory to see actual patterns:
- `backend/Tools/Benchmarks/State/*.cs`
- `backend/Tools/Benchmarks/Messaging/*.cs`
- `backend/Tools/Benchmarks/Infrastructure/*.cs`

`TestGroups.Game` and `TestGroups.Meta` exist; a game may have no benchmarks, directories or docs for them — a case in
those groups then creates `backend/Tools/Benchmarks/Game/` or `Meta/` and a new `GAME.md` / `META.md`.

Read `backend/Tools/Benchmarks/TestGroups.cs` for valid group and subgroup names.

### Step 2 — Analyze the feature code

Find and read the feature's source code:
- Grain interfaces (look for `IGrainWith*Key`)
- Grain implementations
- State classes (`IStateValue`)
- Service interfaces and implementations
- Look in `backend/Game/`, `backend/Meta/`, `backend/Cluster/`, `backend/Infrastructure/`, `backend/Common/`

Identify every meaningful operation: CRUD methods, transaction chains, async workflows, state transitions, message flows.

### Step 3 — Identify gaps

Compare existing benchmarks against the feature's operations. List what's NOT covered yet.

### Step 4 — Propose benchmark cases

For each gap, propose a benchmark case. Avoid duplicating existing benchmarks.

## Output Format

Return a structured list. Each case must include:

```
### [case number]. [proposed title]
- **Group**: State | Messaging | Game | Meta | Infrastructure
- **MetricName**: ops/s | msg/s
- **What it measures**: 1-2 sentences
- **Distributed**: Yes | No
- **Payload fields**: field=type=default (or "EmptyPayload")
- **Template**: closest existing benchmark to follow (file path)
```

## Choosing the right metric

The metric value is always `count / duration` (`BenchmarkState.CalculateMetricValue()`), where `count` is the
number of `handle.Metrics.Inc()` calls. `MetricName` is only the unit label.

| Scenario | MetricName | How counted |
|----------|-----------|-------------|
| Concurrent iterations (throughput) | ops/s | `handle.RunConcurrentIterations(payload, Process)`, `handle.Metrics.Inc()` inside `Process` |
| Message sending/receiving | msg/s (also `req/s`, `update/s` in use) | `handle.Metrics.Inc()` per received message |

## Choosing the right template

| Benchmark type | Template file |
|---------------|--------------|
| Throughput with concurrent iterations | `backend/Tools/Benchmarks/State/StateTest.cs` |
| Transactions | `backend/Tools/Benchmarks/State/TransactionStateTest.cs` |
| Event-sourced state | `backend/Tools/Benchmarks/State/EventStateTest.cs` |
| Messaging throughput (distributed, Node on every service) | `backend/Tools/Benchmarks/Messaging/DurableQueueDirectStressTest.cs` |
| StateCollection sync | `backend/Tools/Benchmarks/Messaging/StateCollectionUpdateStressTest.cs` |
| Infrastructure (task scheduling, no grains) | `backend/Tools/Benchmarks/Infrastructure/TaskBalancerPriorityTest.cs` |
| Side effects | `backend/Tools/Benchmarks/Infrastructure/SideEffectThroughputTest.cs` |
