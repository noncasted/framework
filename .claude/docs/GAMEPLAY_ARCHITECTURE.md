# Gameplay Architecture

**TL;DR:** A gameplay scope is a loop root over a few feature folders. Every thing of the world with its own life
(player, enemy, boss, weapon, a field of obstacles) is an **entity**: a prefab with generated bindings and a
`ScopeEntityView`, its own entity scope with a root, plain component classes and states. A thing with modes of behaviour
has **states** (`IState`), never an enum with branches in one `Tick`. The folders follow the things of the game, not
the layers of the code.

Reference implementation: the reference folders of the project (`.claude/project-skills.md` § architecture); without
them pixel-swarm `client/Assets/GamePlay` (`Players/Entity` for an entity). Build a new feature like its closest
analogue there, not from scratch. Container details: `COMMON_CONTAINER.md` § Entity scope; bindings:
`HIERARCHY_BINDINGS.md`; states: `STATE_MACHINES.md`.

## Layout of a gameplay assembly

```
Gameplay/
  Loop/         the scope root (GameplayScopeExtensions.Construct), the loop of a run (start, end, restart), its HUD
  Players/      Entity/{Root,Components,States,Sync}, Services/ (factory, registry, scope extension)
  <Feature>/    one folder per thing of the game (Arena, Swarm, Obstacles, Coins, ...): its entity or services, options,
                views and sync together
  Services/     services of the scope shared by several features (time, hits, pools, sounds, cameras), one folder each
  <Utility>/    small shared code at the bottom (collisions, randomness, ticks), knowing no feature (P14)
  Common/       tools, tests, telemetry of the assembly
  Assets/       scenes, prefabs, options, generated bindings
```

- A folder is a feature: everything one thing needs lives in its folder (P12). The first level names the things of the
  game and stays short; a new kind of a thing goes inside its feature (`Obstacles/Kinds/Laser`), not next to it.
- **No layer split.** No `Model` / `Views` / `Logic` / `Data` / `Network` folders or assemblies that cut one feature in
  parts. "A pure model without Unity for the tests" is not a reason: components are plain classes with constructor
  injection, testable as they are, and an entity scope loads in a test. "A deterministic simulation" (a checksum, a
  replay, lockstep) is not a reason either: the root of the run owns the order of the tick and calls the tick interfaces
  of its entities in that order.
- The root of the scope only wires: it calls one `Add<Feature>()` per feature. A feature knows no sibling feature; they
  talk through interfaces or events the root provides (P14).

## Entity

An entity is a thing created and destroyed during the game with its own view and behaviour. Not an entity: many similar
objects updated every frame (the ECS world of the game, P7), short pooled views drawn by a service (projectiles, damage
numbers), static data (options, definitions).

```
<Feature>/
  Entity/
    Root/        IX, ILocalX : IX, IRemoteX : IX (network only), the root classes, XLocalView / XRemoteView : ScopeEntityView,
                 XRootExtensions
    Components/  plain classes by feature (Movement/, Look/, Health/, ...), contexts, XComponentsExtensions
    States/      <State>/XLocal<State>.cs (+ XRemote<State>.cs), selectors, XStateDefinitions, XStatesExtensions
    Sync/        XSyncExtensions.AddXSync() (network only)
  Services/      XFactory (Build* roots), the registry of live entities, XScopeExtensions
Assets/Prefabs/X.prefab (+ X_Local, X_Remote variants), Assets/Generated/XBindings.g.cs
```

- **Root.** `IX` holds what every copy exposes (`Lifetime`, the read-only state, `Destroy()`); `ILocalX` adds the parts
  of the owner (input, writers). Registered by `XRootExtensions` with `.WithParameter(builder.ScopeLifetime)`; `Destroy()`
  terminates that lifetime and destroys the view. Code that works on any copy takes `IX`.
- **Prefab and view.** The root of the prefab carries an empty `ScopeEntityView` subclass per variant; generated
  `XBindings : ObjectBindings, IEntityComponent` exposes the hierarchy and registers itself. No other hand-written
  `MonoBehaviour` on an entity prefab.
- **Components.** One behaviour per class (P1), constructor injection of the bindings and of each other through
  interfaces, subscriptions in `OnSetup(lifetime)` (`IScopeSetup`), updates on the updater. Extensions group them:
  shared (`AddXViews`), owner (`AddXLocalComponents`), copy (`AddXRemoteComponents`).
- **Factory.** `XFactory` creates the view from the prefab and calls
  `IEntityScopeLoader.Load(lifetime, parentContainer, view, BuildX)`; each `BuildX` is a local function with
  `[ContainerScopeParent]`. One root per variant (local / remote, per character), chosen by a `switch` before `Load`,
  never inside a root (P9, `COMMON_CONTAINER.md` § One graph per root).
- **Network.** The local root adds `AddLocalEntity(factory)` (or `AddSessionEntity`), the remote one
  `AddRemoteEntity(data)` and the caller `FillProperties(data)`. `AddXSync()` registers one `NetworkProperty` per value
  type and is called in **every** root; only the owner writes. The payload and the value types live in
  `shared/<Game>/Entities/<X>/` with their union tags. A game without a network has the same entity minus `Sync/`,
  payloads and the remote root.
- **Destroy.** `entity.Lifetime.Listen(x.Destroy)`; the entity scope and its container end together, so no component
  outlives the view.

New entity, in order: payload and value types (network) -> `Root` -> prefab, bindings, view variants -> `Sync` ->
components by variant -> states -> factory with its `Build*` roots -> registration in the feature extension.

## States

- A thing with modes (idle, run, jump, hurt, dead) has a state per mode: `XLocal<State> : IState` in `States/<State>/`,
  registered as its own interface and `IState`, a `StateMachine` per entity scope and a selector that enters the
  available state of the highest priority. Everything a state does lives on `handle.Lifetime`: it ends with the state.
- **Not a state machine:** an `enum State` field with `if (State == ...)` / `switch (State)` inside one `Tick` or
  `Update`. Each new mode adds a branch to every such chain (P1). An enum of the states may exist only as a value on the
  wire or in a checksum, mapped to the definitions by `XStateDefinitions.Get(value)`.
- Copies of other clients do not choose: `RemoteStateMachine` enters the state the owner writes into its network
  property; both variants of a state live in its folder (`STATE_MACHINES.md` § Remote copies).
- An AI decides, the states act: a behaviour tree writes an intent, the states move the body and write the properties.
