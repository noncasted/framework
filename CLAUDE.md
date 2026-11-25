# Framework

Shared base of the game projects (mines-leader, peace-data-reborn, pixel-swarm, ...): common reactive primitives,
client core, modules, platforms, forked Unity packages, shared protocol and backend infrastructure.

Every project consumes this repository as a git submodule at `<project>/tools/framework`:
- Unity: `client/Packages/manifest.json` -> `"file:../../tools/framework/client/..."`
- backend: `ProjectReference` -> `../tools/framework/backend/...` (relative to the csproj)
- shared, common: `ProjectReference` / UPM `file:` -> `tools/framework/shared`, `tools/framework/common`

## Layout

```
common/                 reactive primitives of both sides: lifetimes, event sources, viewable properties and collections
                        (assembly Framework.Common, namespace Internal, no Unity); Tests~/ holds its xunit tests
shared/                 protocol and types used by both the Unity client and the backend: Protocol/, Session/, Lobbies/,
                        Matchmaking/, ClientTelemetry/
client/
  core/                 DI container (+ ContainerGenerator), scopes, content bundles, catalogues, the Unity side of the reactive
                        primitives (GameObjectLifetime, UniTask waits, Lifetime Graph), editor tools
  flow/                 base of game loop, loaders and mocks (concrete mocks and the startup scene stay in the project)
  modules/              optional modules: publish, network, browser, audio, animations, cameras, inputs, backend, ui,
                        localization, settings, lobbies, telemetry, ...
  platforms/            platform implementations of modules/publish: web, website, itch, telegram, steam
  packages/             forks of third-party packages (render pipelines, uGUI, web stripping tool, UniTask, MemoryPack,
                        unity-mcp), see FORK.md in each; no Addressables since 0.8
  measurements/         the size of the WebGL build per Unity version and stripping variant: README.md, RESULTS.md, ANALYSIS.md
  project/              Unity project for browsing and compiling the framework without a game: Assets/ links the
                        folders of the framework (core, flow, modules, platforms, shared; common as framework-common, next to the
                        content folder Assets/Common), the forks are packages;
                        Library and the generated sln/csproj stay local. Open client/project, not client (Unity takes
                        client/packages for client/Packages). Solution: Unity -batchmode -projectPath client/project
                        -executeMethod Packages.Rider.Editor.RiderScriptEditor.SyncSolution -quit
backend/
  Common/ Infrastructure/ Cluster/ Console/
  Session/              real-time game sessions (assembly Framework.Session, namespace Game.Session)
  Hosting/              the sessions of a game server and the requests of the meta to it: sessions collection, lobby
                        sessions and codes, lobby service, session requester (assembly Framework.Hosting, namespaces
                        Game.Hosting.Collection below Game.Lobbies below Game.Hosting)
  ClientTelemetry/      telemetry of the clients: sessions and startup traces over the meta socket, storage, read API (namespace
                        Game.ClientTelemetry; rules in Contract/CONTRACT.md; the panel is in Console/Infrastructure/ClientTelemetry)
  ClientTelemetry.TestHost/  the same without a game: Kestrel, a silo, its own PostgreSQL container
  MatchMetrics/         what a game writes about a match: storage, read API (MATCH_METRICS.md; rules in Contract/CONTRACT.md)
  measurements/         the memory of the deployed backends on the Coolify hosts: README.md, RESULTS.md, read-only snapshot tools
deploy/                 registry.json: where each project is deployed; observability/: the logs and traces stack of the VPS
.claude/docs/           docs of the patterns shared by the games, for Claude Code (INDEX.md: principles, keyword table)
.claude/skills/         skills shared by the games, linked from `.claude/skills` of each project
tools/scripts/          scripts shared by the projects; deploy/: `prod`, the deploy registry and the production logs and traces
                        over ssh (skill backend-logs); web-bench/: the headless stand and the devices of the web client
                        (skill device-lab); lightbox/: the image window of Claude Code, `lightbox-show` and `lightbox-pick`
                        (skill visualize), linked into ~/.local/bin by its install.sh
releases/               releases: X.Y.Z.md with the steps a game takes to update, unreleased.md, README.md (format, versions)
Directory.Packages.props  package versions of the dotnet projects; the `Directory.Packages.props` of a project imports it
```

## Rules

1. **No project code here.** Nothing in the framework references a game assembly, a game namespace, game assets or
   game paths. A project plugs in through interfaces, registration extensions and settings assets. Generated
   content (catalogues, `ContainerGraph.AssetMap.cs`, local storage keys, options assets) is written into the project.
2. **Modules are optional.** `core` knows nothing about modules; a module depends on `core` (and on other modules only
   when unavoidable). A project includes only the modules it lists in its manifest.
3. **Forks keep their history readable.** Each forked package starts with a pristine upstream commit; every local
   change is a separate commit with the reason, and `FORK.md` lists the patches. To update a fork: commit the new
   pristine version, then reapply the patch commits.
4. **Built artifacts next to their sources.** Code built outside Unity (ContainerGenerator, trimmed MemoryPack, native
   editor plugins) keeps its sources in `Source~/` of the package that ships the result; the build script writes into
   that package; the result is committed together with its `.meta`.
5. **Keep `.meta` files.** Assemblies reference each other by asmdef GUID; plugin importer settings (`RoslynAnalyzer`
   label, disabled platforms) live in `.meta`. Never regenerate them when moving files.
6. **Comments in English.** Prose with the user in Russian.
7. **Fail fast.** No silent fallbacks, no defensive null checks; unexpected state throws; `catch` only at a boundary that reacts.
8. **A change here affects every project.** After a change, build at least the project you are working in; mention in
   the commit message when a change is breaking (`BREAKING:`). A change a game must act on adds its step to
   `releases/unreleased.md` in the same commit (who, a search command, the change).

## Extension points

What a game registers, implements and may rely on is written next to the code, in the `CLAUDE.md` of the folder:
`common`, `shared` (the protocol), `client/core`, `client/flow`, `client/modules` (every module; `telemetry` and `network` in
their own), `client/platforms`, `backend` (persisted type names), `backend/Infrastructure` (telemetry defaults), `backend/Cluster` (the monolith too), `backend/Console`, `backend/Session`,
`backend/Hosting`, `backend/ClientTelemetry`, `backend/ClientTelemetry.TestHost`, `.claude/skills` (how a game links the shared
skills). Claude Code loads such a file by itself with the first file read or edited in its folder, in the framework and
from a game (`tools/framework/...`) alike. A plan of how a game plugs something in reads the file of that folder first.

This file is loaded in every session of the framework and with the first file of it touched from a game, so it holds only what
every task needs. A contract is changed in the `CLAUDE.md` of its folder, in the same commit as the code; a new one goes to
the folder it describes, not here. A new `CLAUDE.md` in a folder Unity imports (the packages, `client/modules`,
`client/platforms`) is committed with its `.meta`. A file loaded on demand is cut at 40 000 characters: before that it is
split by folder.

## Docs

A game imports the index in its `CLAUDE.md`: `@tools/framework/.claude/docs/INDEX.md` from the root of the project,
`@../tools/framework/.claude/docs/INDEX.md` from `.claude/CLAUDE.md`. A nested `CLAUDE.md` like this one loads only when
files of its folder are touched; the import makes the index visible from the first message. It is loaded in every
session, so the index and the principles stay short and the other docs are read by keyword. A pattern true for every
game goes here, a pattern tied to the assets or rules of one game stays in that game.

@.claude/docs/INDEX.md

## Tests

`tools/scripts/test-all.sh` runs the whole suite on the current tree, uncommitted edits included: `dotnet build
framework.slnx`, then backend, common and `ContainerGenerator.Verify`; next to them `control`, `telemetry` and `deploy` (python, the tests of these skills and of `tools/scripts/deploy`), Unity EditMode (`client/project`, needs
its `Library`) and PlayMode (second copy in `.build/test-all/playmode`, kept between runs). It prints a table stage / time /
tests / result, the failed tests and the logs (`.build/test-all/logs`); any failure gives a non-zero code. It leaves
`Analyzers/ContainerGenerator.dll` alone: after a change of the generator the Unity stages use the committed dll until a
Release build replaces it. The expected numbers: backend 1851, common 96, Verify 188, control 24, telemetry 86, deploy 44, EditMode 1381 (4 of them skipped, the
benchmarks of `ContainerBenchmarkTests`), PlayMode 151 (24 of them skipped, the end to end tests of the telemetry without their host);
the script does not check them, a smaller number is a lost test.

`tools/scripts/test-telemetry-transport.sh` runs those 24 against a real backend: it starts `ClientTelemetry.TestHost` (needs Docker) and takes
`client/project`, so no other editor may have it open.
The host alone, its sockets and flags: `backend/ClientTelemetry.TestHost/CLAUDE.md`.

One suite by hand: `.build/bin/Tests/Debug/net10.0/Framework.Tests` (backend),
`.build/bin/Framework.Common.Tests/Debug/net10.0/Framework.Common.Tests` (common): xunit v3 on the Microsoft Testing
Platform, run the executable, `dotnet test` refuses on the .NET 10 SDK. The projects keep their own tests of the framework
code until they move here.

## Working from a project

The submodule is a normal git repository: commit and push inside `tools/framework`, then commit the new submodule
pointer in the project. Other projects update through the `update-framework` skill: it moves the pointer to a release and
carries out the steps of `releases/<version>.md`. A release is cut by the `release-framework` skill.
