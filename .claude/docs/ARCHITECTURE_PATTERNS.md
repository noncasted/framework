# Architecture Patterns (the base for checking plans)

Purpose: a list of checkable patterns shared by the games on the framework, for the skill that creates or edits the plan of a
task. The format of a line: **if** the task has X -> **the plan must contain** Y. Every pattern has a source
(`DOC.md` § Section). The ids are shared with the `ARCHITECTURE_PATTERNS.md` of a game, the numbers have gaps: the other
patterns (network, UI, tests, editor) are tied to the docs of the game and live there. A source that is not next to this file
(`CLAUDE_MISTAKES.md`, `ENTITIES.md`, `CODEGEN.md`, `CLAUDE.md`) is a doc of the game, see `INDEX.md`; in a game without it
the rule rests on the other sources of the line.

---

## 0. General rules (G)

- **G-1** If the plan adds error handling, a fallback, `try/catch` or null checks -> the plan says explicitly that the
  user asked for the degradation; otherwise throw / log an error through `ILog<T>` (fail fast). `CLAUDE.md` § MOST IMPORTANT RULE.
- **G-2** If the task touches `tools/framework` -> the plan has: read `tools/framework/CLAUDE.md` and the `CLAUDE.md` of the touched folder, commit and push
  inside the submodule, then commit the submodule pointer; an estimate of the effect on the other games. `CLAUDE.md` § Framework.
- **G-3** If a new asmdef appears that references `Internal` (a temporary one, a benchmark, a build script included) ->
  the plan references `Internal.Content` too. `CLAUDE.md` § Framework; `CLAUDE_MISTAKES.md` § G37.
- **G-4** If a type is registered from another assembly and has an `[Inject]` method -> the method is `public`.
  `client/core/CLAUDE.md` of the framework (`[Inject]` methods); `COMMON_CONTAINER.md` § MonoBehaviour Services.
- **G-5** If the plan mixes layers -> the client patterns (container, MonoBehaviour) only in the client, the grain pattern
  only in the backend. `CLAUDE.md` § Critical Rules (8).
- **G-6** If the task moves a type into the framework -> the same step deletes the duplicate of the game (otherwise the
  interface of the game with the same name shadows the one of the framework at registration). `CLAUDE_MISTAKES.md` § G22.
- **G-8** If the task brings new knowledge about the mistakes of the agent -> a step "update the knowledge base"
  (`CLAUDE_MISTAKES.md`, the doc of the area). `CLAUDE.md` § Critical Rules (5).

---

## 1. Client

### 1.0 Gameplay layout (C-LAY)

- **C-LAY-1** If a new gameplay feature -> the folder of the feature follows its closest analogue in the reference of the game
  (`.claude/project-skills.md` § architecture, otherwise pixel-swarm `client/Assets/GamePlay`); the plan names the analogue and
  the folders of the feature. `GAMEPLAY_ARCHITECTURE.md` § Layout of a gameplay assembly.
- **C-LAY-2** If the plan splits a feature by layers (`Model` / `Views` / `Logic` / `Data` / `Network`, a separate assembly of a
  "pure model" for tests or for determinism) -> instead the entity or the services of the feature in one folder; the order of the
  tick is kept by the root of the run. `GAMEPLAY_ARCHITECTURE.md` § Layout of a gameplay assembly.

### 1.1 DI and services (C-DI)

- **C-DI-1** If logic / state / a factory is needed -> a plain class with constructor injection, registered in an
  installer extension `builder.Register<T>().As<I>()`, the installer returns the builder. `DECISION_TREES.md` § 3;
  `COMMON_CONTAINER.md` § Registration API, § Plain Service.
- **C-DI-2** If a service needs subscriptions -> `.As<IScopeSetup>()` and the subscription in `OnSetup(lifetime)`; no subscriptions
  in `Awake`; what the service cannot be used without (its own state, the data it read) is done in the constructor
  (`[Inject]` of a MonoBehaviour), a lifetime there through `IReadOnlyLifetime` / `.WithScopeLifetime()`.
  `COMMON_CONTAINER.md` § Setup, § Common Mistakes; `DECISION_TREES.md` § 2.
- **C-DI-3** If service B depends on the initialization of A -> A gets ready in its constructor, B takes A as a dependency
  (the container builds A before B); any other order is written as a call: before the services in the installer, after the load
  at the owner of the scope (`scope.Resolve<GameLoop>().Start(scope.Lifetime)`). There is one phase (`IScopeSetup`), its order is
  not used, there is no async phase. `COMMON_CONTAINER.md` § Setup.
- **C-DI-4** If a new scope appears -> the root `Construct` is passed as a method group (not a lambda), a child root has
  `[ContainerScopeParent]`, the scene of the scope is chosen (`WithRuntimeScene` / `WithAssetScene` / no scene).
  `COMMON_CONTAINER.md` § Service scope, § Common Mistakes.
- **C-DI-5** If implementations are chosen by a condition -> the variants are different roots through a `switch` at the call site;
  inside a root only a `bool` / a closed enum changes the graph (CINGR005). A platform: `#if` or branches inside one installer (`client/modules/CLAUDE.md` of the framework, publish).
  `COMMON_CONTAINER.md` § Entity scope ("One graph per root"); `CLAUDE_MISTAKES.md` § F2, § F3.
- **C-DI-6** If one scope has several alternatives with a marker interface of a phase -> at most one group of alternatives
  with `IScopeSetup` and the like; a platform service with a phase is registered on every platform and checks the platform itself.
  `CLAUDE_MISTAKES.md` § F37.
- **C-DI-7** `WithParameter` belongs to its registration and wins over a registration of the same type, the parent scope and `IContainer` / `ILifetime` / `IReadOnlyLifetime` of the scope: a parameter takes the value of the exact type, otherwise a value it can accept (several such: CINGR014). `COMMON_CONTAINER.md` § WithParameter belongs to its registration and wins.
- **C-DI-8** If a service needs the assets of a catalogue -> `Request*Group` in the installer (or `Load*Group`, only if
  `Construct` reads the assets itself); no `Retain` in the gameplay; one reference `ContentReference<T>` -> `RequestContent` /
  `LoadContent` in the installer or `IContentLoader.Load` on the lifetime of the owner. `COMMON_CONTAINER.md` § Asset Groups, `CONTENT.md`.
- **C-DI-9** If code takes `IContainer` / `Resolve` -> only factories and tools; services through the constructor;
  the container only on the main thread. `COMMON_CONTAINER.md` § Runtime Access.
- **C-DI-10** If a dependency cycle is possible -> the plan breaks it (an event, `Lazy<>`, a split of the service); in the backend
  a cycle through `As<>` hangs the services without an exception. `COMMON_CONTAINER.md` § Diagnostics (CINGR004); `CLAUDE_MISTAKES.md` § F50.
- **C-DI-11** If a foundation asmdef changes (`Internal`, `Internal.Editor`) -> no references to `Flow`, `Global`,
  `Meta`, `Menu`, `GamePlay`; the editor code of the game goes to the editor asmdef of its layer. `CLAUDE_MISTAKES.md` § F5.

### 1.2 Scene and MonoBehaviour (C-SC)

- **C-SC-1** If an object lives in a scene -> MonoBehaviour + `ISceneService.Create(IScopeBuilder)` +
  `RegisterComponent(this).As<I>()`, dependencies through an `[Inject]` method, the phase through `.As<IScopeSetup>()`.
  `DECISION_TREES.md` § 3; `COMMON_CONTAINER.md` § MonoBehaviour Services; `CLAUDE_MISTAKES.md` § Key Rules (1), § F1.
- **C-SC-2** If a new scene service gets a dependency on it -> the order of the steps: the component in the scene ->
  `Assets/Scan services` -> save the scene -> update the asset map -> only then the dependency in a constructor
  (otherwise CINGR003). `COMMON_CONTAINER.md` § MonoBehaviour Services; `CODEGEN.md` § Bootstrap Order.
- **C-SC-3** If a prefab is instantiated by a service scope (a loading screen, an overlay) -> `builder.Instantiate` +
  `builder.Inject` / `RegisterInstance(...).As<>`. `DECISION_TREES.md` § 3; `COMMON_CONTAINER.md` § MonoBehaviour Services.
- **C-SC-4** If a MonoBehaviour is created at runtime and needs dependencies -> `builder.Injectable<T>()` +
  `IViewInjector.Inject`; `T` has a visible `[Inject]` method (CINGR008). `DECISION_TREES.md` § 3; `COMMON_CONTAINER.md` § Registration API.
- **C-SC-5** If a MonoBehaviour has a handler -> its name does not match a Unity message (`OnPreRender` and the like).
  `CLAUDE_MISTAKES.md` § F44.
- **C-SC-6** If a runtime package with a MonoBehaviour must be excluded from the build -> `defineConstraints: UNITY_EDITOR`, not
  `includePlatforms: Editor`. `CLAUDE_MISTAKES.md` § F38.

### 1.3 Entities (C-EN)

- **C-EN-1** If a new thing with its own life and look (a player, an enemy, a boss, a weapon, a field) -> an entity in the order of
  `GAMEPLAY_ARCHITECTURE.md` § Entity: `Root` (`IX` / `ILocalX` / `IRemoteX`), a prefab with bindings and view variants,
  `Sync`, components per variant, states, a factory with `Build*`.
- **C-EN-2** If the behaviour differs at the owner and at a copy -> separate Local / Remote classes, separate roots and
  prefabs; no `if (IsLocal)` in components. `GAMEPLAY_ARCHITECTURE.md` § Entity (Factory); the exceptions are in the doc of the game.
- **C-EN-3** If an entity replicates state -> one `AddXSync()` called in every root; one property per type of
  value; only the owner writes. `GAMEPLAY_ARCHITECTURE.md` § Entity (Network).
- **C-EN-4** If code is needed on an entity prefab -> only a `ScopeEntityView` subclass and the generated bindings;
  components are plain classes that get the bindings in the constructor. `GAMEPLAY_ARCHITECTURE.md` § Entity;
  `HIERARCHY_BINDINGS.md` (TL;DR).
- **C-EN-5** If a removal / the leave of the owner -> the chain `entity.Lifetime.Listen(x.Destroy)`.
  `GAMEPLAY_ARCHITECTURE.md` § Entity (Destroy).
- **C-EN-7** If an entity has state / behaviour (lifetime, events, update, input) -> it is a component of the container with
  an interface, not a field of a "model"; `Definitions` folders hold only serializable options. `GAMEPLAY_ARCHITECTURE.md` § Entity.

### 1.4 State machines and behaviour trees (C-SM)

- **C-SM-1** If an entity has states (modes of behaviour) -> `IState` + `ObjectStateDefinition`, a class per state in
  `States/<State>/`, the entry through `TryCreateHandleFromPriorityQueue`, every subscription of a state on `handle.Lifetime`; the
  selector on `Exited`. An `enum State` with branches in one `Tick` is not a state machine. `STATE_MACHINES.md` § API, § Entry via
  priority queue, § Selector on Exited; `GAMEPLAY_ARCHITECTURE.md` § States.
- **C-SM-2** If a state is visible to other clients -> a Local / Remote pair in one folder, `RemoteStateMachine`,
  the data in an `IStatePayload` with a union tag; a remote state does not exit by itself. `STATE_MACHINES.md` § Remote copies;
  `GAMEPLAY_ARCHITECTURE.md` § States.

### 1.5 Lifetime (C-LT)

- **C-LT-1** If there is `Advise` / `View` / `Listen` / `ListenClick` -> every subscription has a lifetime; the plan says
  whose. `COMMON_LIFETIMES.md` § Key Rules; `DECISION_TREES.md` § 2.
- **C-LT-2** If a subscription to an item of a collection -> `item.Lifetime` or the lifetime of the entry from `View(lifetime, (entryLifetime, item) => ...)`.
  `DECISION_TREES.md` § 6; `COMMON_REACTIVE_COLLECTIONS.md` § Key Rules; `CLAUDE_MISTAKES.md` § F7.
- **C-LT-3** If nested objects (a character -> its abilities) -> a hierarchy of `Child()`; `Intersect` only when
  the shorter of two unrelated ones is needed. `COMMON_LIFETIMES_PATTERNS.md` § Lifetime Hierarchy, § Lifetime Intersection, § Common Mistakes.
- **C-LT-4** If a one-off operation / a wrapper of a callback -> `new Lifetime()` with a guaranteed `Terminate`;
  in cluster benchmarks a root `new Lifetime()` is forbidden, in tests `Terminate` goes in `finally`.
  `DECISION_TREES.md` § 2; `CLAUDE_MISTAKES.md` § F11.
- **C-LT-5** If `TerminatedLifetime` is used -> only as "already over" (the listeners are called at once),
  not as "not set up yet". `CLAUDE_MISTAKES.md` § F35; `COMMON_LIFETIMES_PATTERNS.md` § Conditional Lifetimes.
- **C-LT-6** If the plan changes the creation of lifetimes in long-lived objects -> a step that checks for leaks with
  `Tools/Lifetime Graph`. `COMMON_LIFETIMES.md` § Lifetime Graph.

### 1.6 Reactive (C-RX)

- **C-RX-1** If a signal without state is needed -> `EventSource`; state -> `ViewableProperty`; a collection ->
  `ViewableList` / `ViewableDictionary`; outwards a read-only interface. `DECISION_TREES.md` § 1;
  `COMMON_REACTIVE_BASICS.md` § Key Rules.
- **C-RX-2** If UI shows a value -> `View` (the current one + the future ones); `Advise` only for events.
  `DECISION_TREES.md` § 5; `COMMON_REACTIVE_VALUES.md` § Common Mistakes; `CLAUDE_MISTAKES.md` § F8.
- **C-RX-3** If an item of a collection is updated in place -> a mutation + `NotifyChangedAt`, not `RemoveAt` + `Add`.
  `COMMON_REACTIVE_COLLECTIONS.md` § Inplace Update.
- **C-RX-4** If the value lifetime of `View((lt, v) => ...)` is used -> do not pass it on for the initial
  (not set) value; a `ViewNotNull` that fires at setup is guarded by a readiness check.
  `CLAUDE_MISTAKES.md` § F9, § F10; `COMMON_REACTIVE_VALUES.md` § Key Rules.
- **C-RX-5** If a reactive value is set from several threads (backend) -> the whole apply under one lock, without
  an await inside. `CLAUDE_MISTAKES.md` § F33.
- **C-RX-6** If a handler of an event may be called inside the calling code (a hit, the death of a body) -> the reaction is
  queued and runs in its own update, the lists of the caller are not touched. `CLAUDE_MISTAKES.md` § G10.

### 1.7 Views and prefabs (C-VW)

- **C-VW-1** If a runtime view is needed -> a prefab: a composite one as children of the entity prefab through bindings; many
  short-lived copies as a pool over a template prefab; no `new GameObject` + `AddComponent`
  outside editor builders and procedural meshes; the plan has a grep check. `HIERARCHY_BINDINGS.md` § Runtime Views
  Come From Prefabs; `CLAUDE_MISTAKES.md` § G36.
- **C-VW-2** If code looks for objects in the hierarchy -> the generated `*Bindings` instead of `transform.Find` / `GetComponentInChildren`;
  one bindings class per type of object, a prefab variant has its own class. `HIERARCHY_BINDINGS.md` (TL;DR), § Rules.
- **C-VW-3** If the hierarchy of an entity prefab changes -> steps: Regenerate bindings -> rescan `ScopeEntityView` ->
  update the asset map; a check that the new references are filled (unchanged bindings are not rebound).
  `HIERARCHY_BINDINGS.md` § Rules; `CODEGEN.md` § Regenerate All.

### 1.12 API and code style (C-API)

- **C-API-1** If an asynchronous method -> `UniTask` / `UniTask<T>` without the `Async` suffix; fire-and-forget is `.NoAwait()`.
  `API_DESIGN_FULL.md` § Rule 1; `DECISION_TREES.md` § 4, § 8; `CODE_STYLE_FULL.md` § UniTask Fire-and-Forget.
- **C-API-2** If a method returns a collection -> `IReadOnlyList<T>`, an empty one instead of null. `API_DESIGN_FULL.md` § Rule 2, § Rule 3.
- **C-API-3** If a callback API -> a UniTask wrapper. `API_DESIGN_FULL.md` § Rule 5.
- **C-API-4** If a new class -> the order of members (constructor -> fields -> public -> private -> local),
  `_camelCase` without abbreviations, collections initialized inline, `TryGetValue`. `CODE_STYLE_FULL.md` § Member Order,
  § Field Naming, § Collections, § Dictionary Lookup.
- **C-API-5** If `WhenAll` -> every task is a named local variable, no inline lambdas.
  (The user memory `named-tasks-before-whenall.md`, not `.claude/docs`.)

---

## 3. Backend (B)

### 3.1 Orleans and grains

- **B-1** If a new grain -> an interface with one key type, `[Transaction]` only on the transactional methods,
  constructor injection, `[State]` / `[EventState]`. `COMMON_ORLEANS.md` § Grain Pattern.
- **B-2** If code outside a grain calls Orleans -> `IOrleans` and the extension methods (`GetGrain`, `InTransaction`);
  inside a grain `GrainFactory` / `Grains`. `COMMON_ORLEANS.md` § IOrleans Interface.
- **B-3** If a grain holds an observer reference of a process that may die -> `[Reentrant]`, the observer is reset
  on a Send error. `CLAUDE_MISTAKES.md` § F15.
- **B-4** If an activation must be kept -> keep-alive on the calls (`AddObserver` / `BindObserver` / `Publish`), not in
  `OnDeactivateAsync`. `COMMON_ORLEANS.md` § Messaging; `MESSAGING.md` § RuntimeChannel; `CLAUDE_MISTAKES.md` § F29.
- **B-5** If the set of services of a role / a module (`IGameModule`) changes -> the constructors of the grains activated
  on that role are checked (`GameCompositionTests`). `COMMON_ORLEANS.md` § Hosts, roles and modules; `CLAUDE_MISTAKES.md` § F32.
- **B-6** If a process wrote the state of a grain itself -> do not poll it for a confirmation; do not keep local
  delivery locks through the binding of an observer. `CLAUDE_MISTAKES.md` § F16, § F17.

### 3.2 State

- **B-7** If a new state -> `[GrainState]` or `[GrainEventState]`, `[GenerateSerializer]`, `[Id(N)]` in a row,
  `Version`; registered by the `StatesLookup` generator; the table by the deploy setup. `COMMON_ORLEANS.md` § State, § Adding New State.
- **B-8** If an event-sourced state -> `Apply` for every event type, `Append` + `Write` as a pair before any await,
  `Append` is asynchronous. `COMMON_ORLEANS.md` § Event-sourced state; `CLAUDE_MISTAKES.md` § F18-F21.
- **B-9** If a state per DeployId -> `IDeployScopedState`, direct only. `COMMON_ORLEANS.md` § Adding New State (5).
- **B-10** If a collection addressed by a key on every service -> `StateCollection` (+ `AddStateCollection`).
  `COMMON_ORLEANS.md` § StateCollection. (See K-6.)
- **B-11** If records must be listed -> only the keys (`ReadAllKeys`), without deserialization. `CLAUDE_MISTAKES.md` § F31.
- **B-12** If the database schema changes -> the migrator step is idempotent (a clean database, the current one, any old one).
  `DEPLOY.md` § (Migrations, the lines about `PostResourcesSetup`).

### 3.3 Messaging

- **B-13** If changes of state are broadcast -> `RuntimeChannel` + an `onGapDetected` handler (reread the source).
  `MESSAGING.md` § RuntimeChannel, § Choosing.
- **B-14** If a command must survive a restart -> `DurableQueue` with an idempotent handler, relying neither on
  the order nor on the delivery to everyone. `MESSAGING.md` § DurableQueue.
- **B-15** If a request to one service with an answer -> `RuntimePipe`, the caller retries itself. `MESSAGING.md` § RuntimePipe.
- **B-16** If a guaranteed delivery to every subscriber is needed -> store the data and let the subscribers read it.
  `MESSAGING.md` § Choosing.
- **B-17** If Orleans messages -> `[GenerateSerializer]` + `[Id(N)]`; the subscription on a lifetime. `COMMON_ORLEANS.md` § Messaging.

### 3.4 Console (Blazor)

- **B-18** If a page of the console -> early return, `[Inject]` in `@code`, reactive pages derive from `UiComponent`.
  `BLAZOR.md` (the rules at the top and the sections about inject / UiComponent).

---

## 4. Shared (S)

- **S-1** If both the client and the backend need a type -> `shared/` (protocol, models, configs); the states of grains only in the
  backend. `CLAUDE.md` § Overview; `COMMON_ORLEANS.md` § Direct state class.
- **S-2** If a payload / a state payload / an event -> MemoryPack, the tag in the partial declaration of the union next to the type,
  a member of a union type with `[MemoryPackAllowSerialize]`. `NETWORK.md` § Union tags; `GAMEPLAY_ARCHITECTURE.md` § Entity (Network).
- **S-3** If session logic -> `Framework.Session.Core` (C# 9, synchronous, without Unity and Microsoft.Extensions); the lists of
  the parts of `SessionCore` and `AddSessionServices` are kept in sync. `NETWORK.md` § Session.
- **S-5** If the backend uses `StableHash` / `ErrorCode` -> the full name `Shared.*` (a conflict with Orleans).
  `NETWORK.md` § NetworkProperty, § Request errors.
