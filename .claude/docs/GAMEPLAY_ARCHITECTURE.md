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

The tree is fixed: these names and these cells only. A plan of a feature writes its own tree in these cells and the
user approves it before the code (`plan-architecture`, Step 4b). A folder outside the tree, or another name, comes only
with a reason in the plan. A cell the feature does not need is not created (no `Entity/` for a feature without an entity).

```
Gameplay/
  Loop/                      the scope root and the run
    GameplayScopeExtensions  Construct: one Add<Feature>() per feature, nothing else
    GameplayLoop             start, end, restart of a run
    Run/ Hud/ Result/ ...    parts of the run that belong to no thing of the game, a folder each
  <Feature>/                 a thing of the game: Players, Enemies, Obstacles, Arena, Coins
    Definitions/             data only: XDefinition (EnvAsset), XOptions, enums of data; no behaviour
    Entity/                  when the thing is an entity (§ Entity)
      Root/                  IX, ILocalX, IRemoteX, LocalX, RemoteX, XLocalView, XRemoteView, XRootExtensions
      Components/<Part>/     plain classes of one part (Movement, Health, Look); XComponentsExtensions next to the folders
      States/<State>/        XLocal<State>, XRemote<State>; XStateDefinitions, XLocalStateSelector,
                             XRemoteStateSelector, XStatesExtensions next to the folders
      Sync/                  XSyncExtensions (network only)
    Services/                XFactory (Build* roots), the registry of the live entities (IXs), services of the feature
                             in folders of their own, XScopeExtensions (Add<Feature>)
    <SubFeature>/            a part with its own data and behaviour (Players/Weapon): the same shape inside
  Services/<Service>/        services of the scope used by several features: Time, Hits, Pools, Sounds, Cameras
  <Utility>/                 small shared code at the bottom (Collisions, Randomness, Ticks), knowing no feature (P14)
  Common/                    Tools/, Tests/, Telemetry/ of the assembly
  Assets/                    Scenes/, Prefabs/, Options/, Generated/ (bindings)
```

- A folder is a feature: everything one thing needs lives in its folder (P12). The first level names the things of the
  game and stays short; a new kind of a thing goes inside its feature (`Obstacles/Kinds/Laser`), not next to it.
- Where a class goes follows from what it is: data -> `Definitions`, the face of the entity -> `Entity/Root`, one
  behaviour of the entity -> `Entity/Components/<Part>`, a mode -> `Entity/States/<State>`, creating and tracking the
  entities -> `Services`. A class that fits no cell is a sign of a missing feature or of a class doing two things.
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

Its files take the cells `Entity/{Root,Components,States,Sync}` and `Services/` of its feature (§ Layout); the prefab
is `Assets/Prefabs/X.prefab` (+ `X_Local`, `X_Remote` variants), the bindings `Assets/Generated/XBindings.g.cs`.

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
