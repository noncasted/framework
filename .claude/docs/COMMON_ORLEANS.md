# Orleans: Full Reference

## Quick Navigation

| Looking for... | Go to section |
|----------------|---------------|
| Grain interface + class boilerplate | Grain Pattern |
| State<T>, EventState<T>, IStateValue, operations | State |
| GetGrain, cross-grain calls | IOrleans Interface |
| Multiple items stored as a collection | StateCollection |
| Push updates to clients | Messaging |
| OnActivateAsync, OnDeactivateAsync | Grain Lifecycle |
| Tables, jsonb, serialization | Storage: PostgreSQL jsonb |

---

## Grain Pattern

```csharp
// Interface — one key type only
public interface IMyGrain : IGrainWithGuidKey
{
    [Transaction]  // only if called inside a transaction
    Task<string> GetValue();

    Task FireAndForget();  // no attribute if not transactional
}

// Implementation
public class MyGrain : Grain, IMyGrain
{
    // Constructor injection — never [Inject] fields
    public MyGrain(
        [State] State<MyState> state,
        IOrleans orleans,
        ILogger<MyGrain> logger) {
        _state = state;
        _orleans = orleans;
        _logger = logger;
    }

    private readonly State<MyState> _state;
    private readonly IOrleans _orleans;
    private readonly ILogger<MyGrain> _logger;

    public async Task<string> GetValue()
    {
        var s = await _state.ReadValue();
        return s.Name;
    }
}
```

**Key types:**
- `IGrainWithGuidKey` — Guid-based identity (most common for aggregates)
- `IGrainWithStringKey` — string-based identity (named singleton-like services)

---

## State

Two kinds of grain state, both implement `IStateValue` (`backend/Infrastructure/Orleans/State/StateInterfaces.cs`):

| Kind | Value interface | Wrapper | Constructor attribute | Storage |
|------|-----------------|---------|-----------------------|---------|
| Direct | `IDirectStateValue` | `State<T>` | `[State]` | Row in a jsonb table (`DirectStorage`) |
| Event-sourced | `IEventStateValue` | `EventState<T>` | `[EventState]` | Marten event stream + inline snapshot (`EventStorage`) |

### Direct state class

```csharp
[GenerateSerializer]
[GrainState(Table = "state_my_entity", State = "my_entity", Lookup = "MyEntity", Key = GrainKeyType.Guid)]
public class MyState : IDirectStateValue
{
    [Id(0)] [JsonProperty] public Guid Id { get; private set; }    // Id(N) sequential, no gaps
    [Id(1)] [JsonProperty] public string Name { get; private set; } = string.Empty;
    public int Version => 0;                 // always required

    public void Rename(string name)
    {
        Name = name;
    }
}
```

The state changes only through its methods. `[JsonProperty]` is required on a private setter: without it Newtonsoft saves
the value but does not restore it, and the property silently comes back as the default after a reload.

`[GrainState]` (`backend/Common/Lookups/GrainStateAttribute.cs`): `Table` = DB table, `State` = value of the
`type` column (several states may share one table, e.g. `cluster`, `configs`), `Lookup` = name of the generated
`StatesLookup.<Lookup>` entry, `Key` must match the grain key type. States are declared only in
the backend: shared has no state attribute (the card game `[SharedGrainState]` configs were removed in F3.3; their
old rows in `configs` stay unread - every query filters by `type`).

### Injecting

```csharp
// In grain constructor — [State] attribute triggers IStateFactory
public MyGrain([State] State<MyState> state) { ... }
```

### Operations (`backend/Infrastructure/Orleans/State/StateExtensions.cs`)

```csharp
// Read + modify + write — returns updated value
var state = await _state.Update(s => {
    s.Rename(name);
});

// Read + modify + write — returns Task
await _state.Write(s => {
    s.Rename(name);
});

// Read only — returns T
var state = await _state.ReadValue();

// Read + transform
var name = await _state.Read(s => s.Name);
```

Inside a transaction `Write()` only records the change; it is committed with the transaction.

A state held by an open transaction (from its first `Read` in the transaction to its commit or rollback) throws
`InvalidOperationException` on `Read` / `Write` (`EventState`: `Read` / `Append` / `Write`) outside a transaction: the memory
holds the uncommitted changes, a direct write would store them even if the transaction rolls back. A grain whose
non-transactional methods may run while a transaction is open on it (`[Reentrant]`) makes them transactional or retries them.

### Event-sourced state

```csharp
[GenerateSerializer]
[GrainEventState(State = "user_entity", Lookup = "User", Key = GrainKeyType.Guid)]
public class UserState : IEventStateValue
{
    [Id(0)] [JsonProperty] public string Id { get; private set; } = string.Empty;
    [Id(1)] [JsonProperty] public string Name { get; private set; } = string.Empty;
    public int Version => 0;

    public void Apply(UserNameChanged e) => Name = e.Name;   // one Apply per event type
}

// Grain constructor: [EventState] EventState<UserState> state
var state = await _state.Apply(new UserNameChanged(name));    // append event(s), returns updated aggregate
var current = await _state.Read();
```

`Id` is the stream id, written by Marten only: the inline snapshot and `AggregateStreamAsync` set it through the identity
member, private setters and `init` included (a get-only `Id` throws on append). The `[JsonProperty]` of a private `Id` is
required: without it the loaded snapshot has an empty `Id` and Marten restarts the snapshot from the new event, so the
earlier events drop out of it (the stream keeps them, `Rebuild` sees them). `Read`, `ReadBatch`, `ReadAll` and `ReadPage` of
`EventStorage` throw on such a snapshot, naming the type and the attribute; a missing stream still reads as a new aggregate.

Snapshot table is `mt_doc_<typename lowercase>` (derived by the generator). Real example: `backend/Meta/Users/Entities/User.cs`.

### Adding New State

1. Mark the state class with `[GrainState(...)]` or `[GrainEventState(...)]`.
2. That is all for registration: the source generator `backend/Tools/Generators/StatesLookupGenerator.cs` emits
   `StatesLookup` (`StatesLookup.<Lookup>`, `StatesLookup.All`) and `GeneratedStatesRegistration.AddAllStates()`,
   which the composition root hands to the hosts (`ClusterComposition.RegisterStates`, see "Hosts, roles and
   modules") and `ProjectsSetupExtensions.AddStates()` feeds into `GrainStatesRegistry`.
   The generator runs only in a host project that sets the MSBuild property `StatesLookupNamespace`
   (`Orchestration/GameComposition` -> `Shared`, `Tools/Tests` -> `Tests`, own test grain states included); other
   projects get nothing, so there are no duplicate public types and no `NoWarn CS0436`. The host collects its own
   states through `ForAttributeWithMetadataName` and the states of referenced backend assemblies (only the ones
   that reference `Common` / `shared`). `UserProjectionSourceGenerator` is opt-in the same way:
   `UserProjectionSourceInterface` (metadata name of the source interface) + `UserProjectionsNamespace`.
   The properties are made visible to the generators in `backend/Directory.Build.props`; tests -
   `backend/Tools/Tests/Codegen/` (snapshots through `CSharpGeneratorDriver`).
3. Direct-state tables are created by the deploy setup tool (`backend/Tools/DeploySetup/StatesSetup.cs`) from
   `DeployStates.Select` = `StatesLookup.All` without the states of the `Benchmarks` assembly unless
   `Benchmarks:Enabled` (the Aspire AppHost sets it for its in-process migrator; production has no benchmark
   tables). Event states are skipped: Marten manages its own tables; its inline snapshot projections are the event
   states of `IGrainStatesRegistry` (`MartenSetupExtensions.Configure`, shared with the test cluster), not an
   AppDomain scan.
4. Collections only: register a `StateCollection` (see below).
5. A state keyed by the DeployId (one record per deploy) implements `Cluster.Deploy.IDeployScopedState`:
   `IDeployCleanup` finds such states in `IGrainStatesRegistry` and removes the records of old deploys.
   It must be a direct state; the cleanup reads only the keys (`IStateStorage.ReadAllKeys`), so the rows of a
   `DeploymentState<T>` (an `AddressableStateValue` under the state name) are cleaned too. A `DeploymentState<T>`
   with the default identity registers `T` with `State = "deployment_state_<T name>"`, `Table = "cluster"`
   (`LiveSessionsData`, `ConnectedUsersLiveData`).

### Hosts, roles and modules (IGameModule)

- `ProjectsSetupExtensions` (`Orchestration/Extensions`, no Game / Meta / Benchmarks references) registers the
  cluster services of a host kind: `SetupCoordinator`, `SetupGateway(role)`, `SetupSilo(role)`,
  `SetupConsole(role)`, each takes a `ClusterComposition` (required roles, generated state registration, modules).
- `IGameModule.ConfigureServices(role, builder)` - every host calls every module once with its own role; a module
  registers only what the role needs. Union types need no module call (`[MemoryPackUnion]` attributes).
- The composition root of this project is `Orchestration/GameComposition`: `GameRoles` (Meta, Game, Silo,
  Console; `ServiceTag.Coordinator` is the only built-in role), `GameModule` (user services in Meta, user
  collection in Silo (the `User` grain) and Console, sessions + `PublishGameServerUrl` in Game), `BenchmarksModule`, and
  `builder.CreateGameComposition()` used by every `Program.cs`. It is also the generator host (`StatesLookup`,
  `GeneratedUserProjections`), so `Tools/DeploySetup` references it.
- Benchmarks are opt-in: `Benchmarks:Enabled` (`Benchmarks__Enabled=true`, set by the Aspire AppHost). Allowed only in
  Development and with a loopback Postgres host - otherwise the host fails at startup. Off: no benchmark
  services, no `/api/benchmarks`, and the silo drops the benchmark grain classes from `GrainTypeOptions`.
  Tests: `backend/Tools/Tests/Deploy/GameCompositionTests.cs` (also: every constructor parameter of a Game / Meta grain
  class is a service of the silo host - `ValidateOnBuild` does not check grains).

---

## IOrleans Interface

Central access point to Orleans from non-grain code (gateways, services):

```csharp
public interface IOrleans
{
    IClusterClient Client { get; }
    ITransactions Transactions { get; }
    IStateStorage StateStorage { get; }
    IStateSerializer Serializer { get; }
    ILogger Logger { get; }
}

// Extension methods (use these, not Client directly):
orleans.GetGrain<IMyGrain>(guid);          // IGrainWithGuidKey
orleans.GetGrain<IMyGrain>("key");         // IGrainWithStringKey
orleans.GetGrain<IMyGrain>(42L);           // IGrainWithIntegerKey
orleans.GetGrain<IMyGrain>();              // IGrainWithGuidKey with Guid.Empty (singletons)
orleans.GetGrains<IMyGrain>(listOfGuids); // batch lookup

// Run code inside a transaction
await orleans.InTransaction(async () => {
    await grainA.DoSomething();
    await grainB.DoSomething();  // both in same transaction
});
```

**Inside a grain** — use the `Grain.GrainFactory` property.

---

## StateCollection

Use when you need a persistent collection of items addressable by key, held in memory on every service and
kept in sync via messaging (durable queue). Real example: `UserCollection` in
`backend/Meta/Users/Entities/UsersCollection.cs`, registered in `UserServicesExtensions.cs`, written by `User`.

```csharp
// 1. State class — the collection value type must be a registered state (IStateValue)

// 2. Interface
public interface IUserCollection : IStateCollection<Guid, UserState> { }

// 3. Implementation — minimal, base class does everything
public class UserCollection(StateCollectionUtils<Guid, UserState> utils, ILogger<UserCollection> logger)
    : StateCollection<Guid, UserState>(utils, logger), IUserCollection;

// 4. Grain that writes to the collection
await _collection.OnUpdatedTransactional(state.UserId, state);  // inside a transaction
// or: await _collection.OnUpdated(key, value);                  // outside a transaction
```

### Registration

```csharp
// In a service extension method:
builder.AddStateCollection<UserCollection, Guid, UserState>()
       .As<IUserCollection>();
```

### Usage (read access from services)

```csharp
var item = _collection[itemId];                    // dictionary access
var all = _collection.Values;                      // all items
_collection.Updated.Advise(lifetime, OnChange);    // subscribe to changes (IViewableDelegate)
```

`IStateCollection<TKey, TValue>` extends `IReadOnlyDictionary`, so it can be used directly as a dictionary.
It loads all existing items on startup (`ILocalSetupCompleted`) via `IStateStorage.ReadAll` and stays in sync
via `IMessaging` durable queue updates.

---

## Messaging

`IMessaging` (`backend/Infrastructure/Messaging/Messaging.cs`) exposes three transports; use the extension methods
from `backend/Infrastructure/Messaging/MessagingExtensions.cs`:

| Transport | Id type | Send | Receive |
|-----------|---------|------|---------|
| Durable queue | `IDurableQueueId` | `PushTransactionalQueue(id, msg)` (void, inside a transaction, sent as a side effect on commit), `PushDirectQueue(id, msg)` | `ListenDurableQueue<T>(lifetime, id, handler)` |
| Runtime pipe (request/response) | `IRuntimePipeId` | `SendPipe<TResponse>(id, msg)`, `IsPipeExists(id)` | `AddPipeRequestHandler<TRequest, TResponse>(lifetime, id, handler)` |
| Runtime channel (pub/sub) | `IRuntimeChannelId` | `PublishChannel(id, msg)` | `ListenChannel<T>(lifetime, id, handler, onGapDetected)` |

```csharp
// Inside a transaction — not awaited, returns void
_messaging.PushTransactionalQueue(queueId, new MyUpdateMessage { ... });

// Subscribe (backend service or gateway)
await _messaging.ListenDurableQueue<MyUpdateMessage>(lifetime, queueId, OnUpdate);
```

**Lifetime** is used here — it exists in backend too, not only in client. Subscription lives as long as the lifetime is active.

Messages are Orleans-serialized: mark them `[GenerateSerializer]` with sequential `[Id(N)]`.

**Delivery guarantees differ per transport** (durable queue: at least one observer, at least once; channel:
per-observer ordered broadcast with catch-up; pipe: one observer). Read `MESSAGING.md` before relying
on a message reaching every subscriber.

`DelayDeactivation` called inside `OnDeactivateAsync` has no effect: request keep-alive on the calls that should keep
the activation (see `RuntimeChannel.KeepAlive`).

**StateCollection, AddressableState, DeploymentState use messaging internally.** You only write messaging
directly if building a custom sync mechanism.

---

## Grain Lifecycle

Standard Orleans overrides: `OnActivateAsync(CancellationToken)` and
`OnDeactivateAsync(DeactivationReason, CancellationToken)`. Examples in this repo: the messaging grains
`backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`, `Queues/DurableQueue.cs`, `Pipes/RuntimePipe.cs`.

Use `OnActivateAsync` for: starting timers, loading supplementary data not in state.
Use `OnDeactivateAsync` for: flushing pending writes, releasing observers.

---

## Storage: PostgreSQL jsonb

Grain state is not stored through Orleans grain storage providers: `State<T>` / `EventState<T>` read and write
through `IStateStorage` (`backend/Infrastructure/Orleans/State/StateStorage.cs`), which dispatches to
`DirectStorage` (direct states) or `EventStorage` (Marten, event states).

### Direct state tables

One table per `[GrainState(Table = ...)]` name (created by `backend/Tools/DeploySetup/StatesSetup.cs`):

| Column | Purpose |
|--------|---------|
| `key` | Grain key: `uuid`, `bigint` or `character varying(512)` depending on `GrainKeyType` |
| `extension` | Only for `IntegerAndString` / `GuidAndString` keys |
| `type` | `State` name from the attribute (discriminator, several states per table) |
| `value` | `jsonb` payload |
| `version` | State version (`IStateValue.Version`, used by `StateMigrations`) |

Primary key is `(key, type)` or `(key, type, extension)`. Writes are `INSERT ... ON CONFLICT` batches
(`DirectStorage.BuildWriteCommands`), so a transaction can put them into one `NpgsqlBatch`.

### Serialization

`IStateSerializer` (`backend/Infrastructure/Orleans/State/StateSerializer.cs`) — Newtonsoft.Json with
`JsonStateSettings.CreateBase()` (`TypeNameHandling.All`, `DefaultValueHandling.Include`).
`JsonUtils.Settings` / `JsonUtils.Deserialize` (`backend/Common/Extensions/JsonUtils.cs`) are used for configs.
`PostgresJsonbConverter<T>` (`backend/Common/Extensions/PostgresJsonbConverter.cs`, jsonb `0x01` version byte) exists
but is currently not used anywhere. There is no `GrainStateStorage` / `OrleansStorage` table — Orleans storage
providers are not used for grain state.

### IStateStorage API (main members)

```csharp
Task<T> Read<T>(StateIdentity identity);
Task<IReadOnlyDictionary<TKey, TValue>> ReadBatch<TKey, TValue>(IReadOnlyList<StateIdentity> identities);
IAsyncEnumerable<(TKey, TValue)> ReadAll<TKey, TValue>(IReadOnlyLifetime lifetime);
Task Write(StateWriteRequest request);
Task Delete(StateDeleteRequest request);
Task<string> ReadRawJson(StateIdentity identity);
// GrainId overloads: StateStorageExtensions.Read<T>(GrainId), Write(GrainId, IStateValue)
```

---

## Key Files

| File | Purpose |
|------|---------|
| `backend/Common/Lookups/GrainStateAttribute.cs`, `GrainEventStateAttribute.cs` | State registration attributes |
| `backend/Tools/Generators/StatesLookupGenerator.cs` | Generates `StatesLookup` and `GeneratedStatesRegistration` |
| `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` | Cluster services by host kind, `AddStates()` builds `GrainStatesRegistry` |
| `backend/Orchestration/GameComposition/GameModule.cs` | Composition root: game services by role, `CreateGameComposition()` |
| `backend/Infrastructure/Orleans/State/StatesRegistry.cs` | `IGrainStatesRegistry`, `GrainStateInfo` |
| `backend/Infrastructure/Orleans/State/Direct/State.cs` | `State<T>` implementation |
| `backend/Infrastructure/Orleans/State/Events/EventState.cs` | `EventState<T>` implementation |
| `backend/Infrastructure/Orleans/State/StateExtensions.cs` | `Update()`, `Write()`, `ReadValue()`, `Read()` helpers |
| `backend/Infrastructure/Orleans/State/StateStorage.cs` | `IStateStorage` |
| `backend/Infrastructure/Orleans/State/Direct/DirectStorage.cs` | jsonb table read/write |
| `backend/Infrastructure/Orleans/State/Events/EventStorage.cs` | Marten event storage |
| `backend/Infrastructure/Orleans/Transactions/` | `[Transaction]`, `ITransactions`, transaction handler |
| `backend/Infrastructure/Data/Collections/StateCollection.cs` | `StateCollection<TKey, TValue>` base |
| `backend/Infrastructure/Data/State/AddressableState.cs` | Singleton persistent `IViewableProperty<T>` synced via channel |
| `backend/Infrastructure/Orleans/Utils/OrleansUtils.cs` | `IOrleans` implementation + extension methods |
| `backend/Infrastructure/Messaging/MessagingExtensions.cs` | Messaging extension methods |
| `backend/Cluster/Deploy/DeploymentState.cs` | Deploy-scoped persistent `IViewableProperty<T>` (table `cluster` + channel) |
| `backend/Cluster/Deploy/ServiceSlicedState.cs` | DeploymentState written by several services: one slice per service id via the `ServiceSlicesWriter` grain |
| `backend/Meta/Users/Telegram/TelegramUserLink.cs` | Grain + `State<T>` example |
| `backend/Meta/Users/Entities/User.cs` | Grain + `EventState<T>` + StateCollection example |
| `backend/Meta/Users/Entities/UsersCollection.cs` | StateCollection example |
