---
name: benchmark
description: Write new benchmarks for backend features using the project's cluster benchmark framework. Use this skill whenever the user asks to benchmark, performance-test, or measure throughput of any backend feature — grains, state, transactions, messaging, game logic, meta systems, infrastructure, task scheduling, or any other backend functionality. Also trigger when the user says "benchmark this", "add benchmark coverage", "measure performance of", or mentions benchmarking backend code.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /benchmark, benchmark, бенчмарк, напиши бенчмарк, добавь бенчмарк, benchmark this, measure performance, протестируй производительность, напиши benchmark
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Write Benchmarks

This skill writes benchmarks for backend features using the cluster benchmark framework in `backend/Tools/Benchmarks/`.

It orchestrates 3 agents in sequence:
1. **Case collector** — analyzes the feature and proposes benchmark cases
2. **Writer** — writes the benchmark .cs files
3. **Docs updater** — updates documentation in `backend/Tools/Benchmarks/docs/`

## Execution Steps

### Step 1 — Collect benchmark cases

Launch the `benchmarks-case-collector` agent with the user's feature/area description.

```
Read agents/benchmarks-case-collector.md for the full agent prompt.

Input: the user's description of what to benchmark (e.g. "messaging", "event state", "task balancer")
Output: structured list of benchmark cases
```

### Step 2 — Confirm with user

Present the case list to the user in a table:

```
| # | Title | Group | Metric | Distributed | Description |
```

Ask: "These are the benchmark cases I'll write. Want to add, remove, or change any?"

Wait for confirmation before proceeding.

### Step 3 — Write benchmarks

Launch the `benchmarks-writer` agent with the confirmed case list.

```
Read agents/benchmarks-writer.md for the full agent prompt.

Input: confirmed list of benchmark cases + the feature code context
Output: new .cs files in the correct group directories under backend/Tools/Benchmarks/
```

### Step 4 — Verify build

Run `dotnet build backend/Tools/Benchmarks/Benchmarks.csproj` and fix any compilation errors.
New `Root` / `Node` classes need no registration: `BenchmarksModule` (`backend/Tools/Benchmarks/Common/BenchmarksModule.cs`)
auto-discovers every `IClusterTest` and every `BenchmarkNode<>` subclass in the Benchmarks assembly; they appear on
the Console Benchmarks page. Benchmarks are opt-in: the Aspire AppHost sets `Benchmarks__Enabled=true` for the dev
cluster; outside Development or against a non-local database the hosts refuse to start with the flag.

### Step 5 — Update documentation

Launch the `benchmarks-docs` agent to update benchmark docs.

```
Read agents/benchmarks-docs.md for the full agent prompt.

Input: paths to newly created benchmark files
Output: updated docs in backend/Tools/Benchmarks/docs/
```

### Step 6 — Report

Tell the user what was created:
- List of new benchmark files with paths
- Build status
- Updated doc files
