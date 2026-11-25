# Architecture Principles

The general rules a plan of a feature or a refactoring is checked against. Each rule is short on purpose; the
details live in the linked docs.

1. **One thing - one class, a composer on top.** Independent effects, looks, systems are separate classes;
   a small composer registers and wires them (one registration extension over a folder of effects). No "god" class that holds several effects and branches by kind (`switch (kind)`).
   Modes of behaviour are states: a class per state (`STATE_MACHINES.md`), not an `enum State` with branches in one `Tick`.
2. **Subscribe, do not poll.** A behaviour starts in `OnSetup` from an event or an observable (`ViewActive`,
   `ListenFirstPick`, `EventSource`) and lives on that lifetime; no per-frame `Has(...)` / value checks to find out
   whether it is on. Every subscription has a named lifetime. (`COMMON_LIFETIMES.md`, `COMMON_REACTIVE_BASICS.md`)
3. **Views and UI are prefabs.** Runtime objects and UI screens come from prefabs with HierarchyBindings or from a
   `PrefabPool<T>` (core, `Runtime/Services/Pools`); no `new GameObject` / `AddComponent` / layout in code. Look (sprites, colours, sizes, sorting) lives
   in the prefab, behaviour numbers live in options. (`HIERARCHY_BINDINGS.md`)
   Exception: what the framework shows without an asset of a game is built from code (the latency overlay of
   `network`, the outdated client notice of `ui`).
4. **Data is assets with references.** One EnvAsset per entity (an upgrade, a kind of enemy); references
   between assets, not indices or string names; no writers that regenerate data from code - the assets are the source.
   Shared visual resources go to catalogues (sprites, colours). (`CODEGEN.md`)
5. **Network ids are explicit.** A network id is an explicit field of the asset (`_id`), never the position in a
   list; a new asset takes the next free id; uniqueness is checked at load (throw) and by a test. Existing values never
   change.
6. **Data changes only through methods.** No public setters or public mutable fields anywhere, Orleans states, tests and
   benchmarks included: state is changed by intent methods (`Init`, `Tick(delta)`, `Restart`, `Hit`), fields are
   `{ get; private set; }`; values fixed after creation are `readonly struct` / readonly / `{ get; init; }`. The full
   list of exceptions: `CODE_STYLE_FULL.md`.
7. **Simulation-heavy state is ECS.** Many similar entities updated every frame live in a shared
   ECS world: components are data with methods, systems are plain classes with `Run(...)`, the old facade keeps
   its external API. Entity handles are not kept between frames.
8. **Fail fast, catch at boundaries.** No defensive null checks, empty catches or silent fallbacks; an unexpected state
   throws. `catch` stands only at a boundary that really reacts: network, files, the top of a task nobody awaits, the
   isolation of subscribers. (`CODE_STYLE_FULL.md`)
9. **Variants are separate roots.** Local / remote / host / guest are different classes or roots, not branches inside
   one class. The only exceptions are written in a doc of the game; the usual one is an entity whose owner changes during
   its life. Any other branch on `IsLocal` / `IsOwner` / `IsHost` is a finding.
10. **Behaviour is preserved and proven.** A refactoring takes a "before" reference before touching code (data dump,
    deterministic harness with md5, frames rendered without stealing focus) and compares "after" to it; tests are written
    on the old code first. Every intended behaviour change is listed explicitly.
11. **Small classes, small files.** A file of runtime code or of an editor tool above 300 lines, or a class with more than
    one reason to change, is split (see P1); generated code and tests have no limit. One top-level type per file, the file
    named after it. A refactoring plan lists the oversized classes of the touched area and how each is cut.
12. **Folders by feature, no clutter.** A folder holds one feature: its entity, components, views, options and
    extension; never split by layer (`Model` / `Views` / `Logic` folders or assemblies, `GAMEPLAY_ARCHITECTURE.md`);
    no "misc" / "utils" dumps, no stray files of another feature, no leftovers after a move (old folders, `.meta` without a file, temporary harnesses or scripts in `client/Assets`). Editor-only code lives under `Editor` /
    `Tools` assemblies, art sources under `Source~`.
14. **Top-down: root, features, shared utilities.** Design and read code from the top: a root / composer (scope
    extension, facade, entity root) wires the feature classes; each feature class owns one behaviour and depends only
    downwards; at the bottom are small shared utilities used by several features (helpers like a hit area,
    a timed bonus, `PrefabPool<T>` of the core). Dependencies point down only: a utility knows no feature, a feature knows no sibling
    feature or its composer (they talk through interfaces or events the composer provides). A plan is written in the
    same order: the root and its wiring first, then the features, then the utilities extracted from what they repeat.
13. **No dead or duplicated code.** Unused classes, methods, fields, options, writers and docs are deleted, not kept
    "just in case"; logic repeated in two places (local and remote, two effects) is extracted once. A plan greps for
    callers before deleting and lists what it removes.
