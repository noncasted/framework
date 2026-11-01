# Benchmarks Writer Agent

Write benchmark .cs files for confirmed benchmark cases, following existing patterns exactly.

## Input

You receive:
1. A list of confirmed benchmark cases (title, group, metric, payload fields, template reference)
2. Context about the feature being benchmarked (grain interfaces, state classes, etc.)

## Process

### Step 1 — Read the template

For each benchmark case, read the template file specified in the case definition. Understand its exact structure: imports, class nesting, constructor signature, payload class, Run() method.

### Step 2 — Read framework base classes

Read these to understand the API (all under `backend/Tools/Benchmarks/`):
- `Common/BenchmarkRoot.cs` — `BenchmarkRoot<TPayload>`: constructor `(ClusterTestUtils utils)`, `Group`, `Subgroup`, `Title`, `MetricName`, `Run(BenchmarkNodeHandle, TPayload)`, `Messaging`, `Cleanup`, `Logger`
- `Common/BenchmarkNodeHandle.cs` — handle passed to `Run()`: `Progress`, `Lifetime`, `CancellationToken`, `Metrics`, `StartNode(ServiceTag, name, payload)`
- `Common/BenchmarkMetricsHandle.cs` — `Metrics.Inc()`; metric value = count / duration
- `Common/BenchmarkNode.cs` — `BenchmarkNode<TPayload>` base for distributed nodes (only if distributed)
- `TestsExtensions.cs` — `RunConcurrentIterations` extension
- `Common/ConcurrentIterationTestPayload.cs` — `IConcurrentIterationTestPayload`
- `Common/TestCleanup.cs` — cleanup tracking
- `Common/TestAssert.cs` — assertion helpers

### Step 3 — Write each benchmark file

For each case, create a .cs file in the correct group directory.

#### Mandatory structure

Every benchmark file must follow this pattern (taken from `State/StateTest.cs`):

```csharp
using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;
// ... other imports as needed

namespace Benchmarks;

public class {Feature}{Type}Test
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload  // only for throughput
    {
        [Id(0)] public int Iterations { get; set; } = 100;
        [Id(1)] public int Concurrent { get; set; } = 10;
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans) : base(utils)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        public override string Group => TestGroups.State;  // use correct group
        public override string Title => "feature-type";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                // one operation
                handle.Metrics.Inc();
            }
        }
    }
}
```

Test grains and their states live inside the benchmark class (see `StateTest.TestGrain` / `StateTest.TestState`):
the state needs `[GrainState(...)]` with a unique `Lookup`, the grain a unique `[GrainType("bench-...")]`.

#### Constructor rules

- First param ALWAYS `ClusterTestUtils utils`, pass to `base(utils)`
- Additional params after it: `IOrleans orleans`, `ITransactions transactions`, etc. (resolved from DI)

#### Metric reporting

- Call `handle.Metrics.Inc()` once per completed operation / received message
- `RunConcurrentIterations` only drives the loop and progress — it does not count by itself
- Distributed: the `Root` listens / collects and calls `Inc()`, `Node` classes (`BenchmarkNode<TPayload>`,
  `protected override string Name`, `Run(IReadOnlyLifetime, TPayload)`) are started with `handle.StartNode(...)` —
  template `Messaging/DurableQueueDirectStressTest.cs`

#### Payload rules

- `[GenerateSerializer]` on every payload class
- `[Id(N)]` on every field, sequential starting from 0
- For throughput: implement `IConcurrentIterationTestPayload` (requires `Iterations` and `Concurrent`)
- For no-config: use `StateMigrationTest.EmptyPayload`

#### Cleanup rules

- Call `Cleanup.Track<TState>(key)` for every grain state created (`Guid` or `string` key)
- Cleanup executes automatically after Run() completes

#### Registration

None: `BenchmarksModule` auto-discovers `IClusterTest` implementations and `BenchmarkNode<>` subclasses (only with
`Benchmarks:Enabled`, set by the Aspire AppHost). Start a node on every role with `handle.StartNodeOnEveryRole(name, payload)`;
benchmarks do not depend on Game / Meta (infrastructure only).

#### File naming

- Pattern: `{Feature}{Type}Test.cs` (e.g. `EventStateTest.cs`, `TransactionLargeBatchTest.cs`)
- Place in correct directory: `backend/Tools/Benchmarks/{Group}/`

### Step 4 — Verify build

Run `dotnet build backend/Tools/Benchmarks/Benchmarks.csproj` and fix any errors.

## Output

Report created files with their full paths.
