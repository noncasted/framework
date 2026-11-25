# Releases of the framework

A game stands on a release, not on a random commit, and gets the list of what to change on its side. The source of truth: the tag
`vX.Y.Z` and the file `X.Y.Z.md` next to this one. A game is updated by the skill `update-framework`, a release is cut by the skill `release-framework`.

| Version | Date | Contents | What a game does |
|--------|------|------------|-----------------|
| [0.1.0](0.1.0.md) | 2026-11-02 | Audit of the client, big files cut, no public setters | Yes: the client API, the backend tests |
| [0.2.0](0.2.0.md) | 2026-11-02 | Fixes of channels, transactions, lobbies and the close of connections, the backend cut, `control` copies | Yes: the backend tests, `.gitignore`, `control` |
| [0.2.1](0.2.1.md) | 2026-11-03 | The skill `media-cleanup`, the commands of `update-framework` in zsh | Yes: a link to the skill `media-cleanup` |
| [0.2.2](0.2.2.md) | 2026-11-03 | `deploy-client`: the argument `--notest` | No |
| [0.3.0](0.3.0.md) | 2026-11-03 | Telemetry of the clients (optional), the stages of the startup trace | Yes: `GameProfiler.Stage` |
| [0.4.0](0.4.0.md) | 2026-11-04 | Telemetry: frame, WebGL, page, previous session; the network without allocations; screen canvases; WebGL stripping; `deploy-client` in parallel, `device-lab` | Yes: `IScreenCanvasCamera`, the font of `NetworkOptions`, the network API, stripping, `deploy-client.json`, `ILobbyMatches.Create` |
| [0.5.0](0.5.0.md) | 2026-11-06 | The modules `resolution` and `framerate`, `Internal.TestTools`, the backend packages, WebRTC with channels and `PeerMesh`, sprites from modules, `audit-swarm` | Yes: the modules from pixel-swarm, `Aspire.AppHost.Sdk`, `InjectGameObject` |
| [0.5.1](0.5.1.md) | 2026-11-07 | `deploy-client`: the argument `--platforms=` | No |
| [0.6.0](0.6.0.md) | 2026-11-09 | Console: online by unique users, Home without the infrastructure cards; attempts to connect to Postgres; the offer of a peer in `PeerMesh`; hooks of `deploy-client` | Yes: `HomeInfrastructureSection` -> `HomeClusterSection`, `AddOnlineTracker`, `OnlineWidget`, the state tests |
| [0.7.0](0.7.0.md) | 2026-11-15 | Audit of the backend: the contract of a connection, `Reserve` with a reason, cluster states through methods, meters at the features, `AddConsoleInfrastructure`; the module `match-metrics`; the reconnect window of a host; Regenerate All with a validator | Yes: gateways and `Closed`, `Reserve`, `OperationProgress`, `ServiceDefaults`, `RequeueStuck`, console, the backend tests, bindings |
| [0.8.0](0.8.0.md) | 2026-11-16 | Content on the AssetBundles of the engine instead of Addressables (`ContentBundle`, `IContentLoader`, `ContentReference` / `ContentScene`); the core without uGUI and `Framework.Shared`; telemetry over HTTP; the package `layout`; `CINGR017` | Yes: content and the manifest, asmdef `Internal.UGUI` / `Internal.Layout` / `Internal.Telemetry.Socket`, WebGL templates and nginx, `brotli` |
| [0.9.0](0.9.0.md) | 2026-11-17 | Content: bundles out of the build (`build.Exclude`), shared bundles of the game (`IContentSharedBundles`), an error on a dirty scene; `moves` in `update-override-packages` | Yes: `BeforeContentBuild(ContentBuildContext)`, an own sync of the shared bundles -> `IContentSharedBundles` |
| [0.9.1](0.9.1.md) | 2026-11-17 | `deploy-client`: the content check reads the catalog from the unpacked `data.unity3d` | No |
| [0.9.2](0.9.2.md) | 2026-11-17 | Content layout: an asset needed by the assets and scenes of one bundle and by another bundle is a duplicate, not a hidden dependency on the bundle with the scenes | Yes: resync the shared bundles, drop the manual roots of the preloaded bundle |
| [0.9.3](0.9.3.md) | 2026-11-17 | Fixes of the audits of the backend and the client of 2026-11-16: a dirty write outside a transaction is an error, `SharedViewableProperty<T>`, the lobbies of a failed server, the catalogue generators fail on a broken input, P2P network, dead code | Yes: `DbLookup` -> `SideEffectTables`, the fakes of `IClusterParticipantContext`, `Game.Hosting.Collection`, `IsJoinable`, `TraceExtensions.Start`, `MockEntry.Process` -> `UniTask` |
| [0.10.0](0.10.0.md) | 2026-11-17 | The startup trace keeps the backend trace id of the socket, the console draws the backend trace from the Aspire dashboard; `GameProfiler.Background`; the web load of content only in the WebGL player | Yes: `AcceptStartup(trace, backendTrace)` |
| [0.11.0](0.11.0.md) | 2026-11-18 | The content code laid out by role, new names (`IContentLoadersCollection`, `ContentEntryPoint`, distributions); the phase `IContentLayoutStep`; `update-framework` offers the shared skills not linked | Yes: `IContentBundles` -> `IContentLoadersCollection`, `ScopeLoadOptions.ServiceScene`, `ContentEntryPoint.RegisterMain` |
| [0.12.0](0.12.0.md) | 2026-11-20 | The ping overlay in its own assembly `Internal.Network.Overlay`: `Internal.Network` without uGUI / TMP | Yes: `NetworkOverlayOptions`, `AddNetworkLatencyOverlay` in the session scope |
| [0.13.0](0.13.0.md) | 2026-11-24 | A queue of the game in the quick search; MemoryPack of the client without rare collections; Web Stripping Tool 1.4.1 and new submodules; `GAMEPLAY_ARCHITECTURE.md`, `/orchestrate --architecture`; Lightbox | Yes: `ISessionSearch.GetOrCreate(userId, queue, ...)`, `ISessionRequester.Request(userId, queue)`, golden files and the protocol hash |
| [0.13.1](0.13.1.md) | 2026-11-25 | The sprite catalogue: the clips of a PNG strip (Multiple Animations), `EditorCatalogues.Bind` for catalogues outside Play mode | No |
| [0.14.0](0.14.0.md) | 2026-11-25 | One phase of a scope `IScopeSetup`, the order by explicit calls; saves load explicitly (`ISaves.Load`, `AddSaves`); the end of an entity reaches its listeners before the container and the assets; `CINGR018`; the lobby code comes back from a match to a new lobby; network events without a source are not lost | Yes: scope stages -> constructor / `OnSetup` / explicit calls, `scope.Initialize()` -> `Start`, `await AddItchSaves()`, `ISaves.Load` in fakes, `PassToMatch(…, leaderId, userIds)` |
| [0.15.0](0.15.0.md) | 2026-11-25 | The client logger: `ILog<T>` from the generator, templates without allocations below the level, `Debug` outside release, levels in `OptionsContainer`; the runtime of the framework on it; telemetry errors with the category and the template, the marks `log.*` | Yes: every `Debug.Log*` of the game -> the logger, log levels, golden files and `ProtocolHash`, `Logs.Of<X>()` in `new` of the network and module types, `EnableLogs` |
| [0.15.1](0.15.1.md) | 2026-11-25 | Notes: step 4 of 0.15.0 is required (every `Debug.Log*` of the game -> the logger), `LOGGING.md` | No (step 4 of 0.15.0 by its new wording) |
| [0.16.0](0.16.0.md) | 2026-11-25 | `PrefabPool<T>` in the core (taken for a lifetime), the agents of `audit-swarm` in the skill, `start-cluster` without its ports table, editor tools on the logger, docs in English; analyzers not shipped | Yes: drop `.omp`, the ports of `start-cluster` in the notes of the game, an own `PrefabPool` may clash |
| [0.17.0](0.17.0.md) | 2026-11-25 | The Roslyn analyzers of the code rules shipped (FWR001-FWR006 as warnings, third-party code skipped by path), generators write `// <auto-generated/>`; the OTel defaults of the hosts in the framework, `ConfigsInitializer.Init` from the content root, `AddMetricsSnapshots`, the monolith runner (optional), the console traces from the Aspire dashboard or VictoriaTraces; the observability stack of the VPS, `backend-logs` | Yes: third-party code under `Assets/Plugins`, Regenerate All, the dll in `backend/Directory.Build.props`, severities, `AddTelemetryDefaults`, `ConfigsInitializer.Init(environment, ...)` and `config.runtimePipe.json` / `config.runtimeChannel.json`, `AddMetricsSnapshots`, `AddConsoleAspireDashboardTraces` |
| [0.18.0](0.18.0.md) | 2026-11-25 | The analyzer dll in the backend images and deploy mirrors, `/ready` from the framework without health spans, the monolith runner throws the real reason of a stopped host; optional deploy on the observability stack and monolith recipe; one top-level type per file in the framework (FWR003) | Yes: `!tools/framework/client/core/Analyzers/Framework.Analyzers.dll` in `.dockerignore` and the deploy mirror, `TelemetryInstrumentation.ReadyEndpointPath` / `IsHealthCheck` |
| [0.18.1](0.18.1.md) | 2026-11-25 | The skills without oh-my-pi: `audit-swarm` on Claude Code agents in the OpenCode Go profile (`clo`), `context_meter.py` finds the transcripts of that profile | No |
| [0.18.2](0.18.2.md) | 2026-11-26 | The folder tree of a task: the fixed cells of a gameplay assembly in `GAMEPLAY_ARCHITECTURE.md`, `plan-architecture` asks to approve the tree, `orchestrate --architecture` starts no code before it | No |

## Versions

`0.MINOR.PATCH` while the API changes freely:

- `MINOR`: a game has something to do (an API, an asset, a setting, a new required dependency, a behaviour it relies on).
- `PATCH`: an update without actions, only the pointer moves.

## How to update a game

If the skill is already linked: `/update-framework` (or `/update-framework 0.2.0`, `/update-framework main`).

In a game whose pointer is older than the skill, from the root of the project:

```
git -C tools/framework fetch --tags origin
git -C tools/framework show v0.1.0:.claude/skills/update-framework/SKILL.md
```

This is an instruction for an agent: give it this output and ask it to carry it out. Step 6 of the skill creates the link.

## How to record changes

A change that makes a game do something adds its step to [unreleased.md](unreleased.md) in the same commit. The file has
`merge=union`, parallel branches do not conflict. `BREAKING:` in the commit message stays (rule 8), but the games read the steps.

## The format of a release file

```markdown
---
version: 0.2.0
date: 2026-11-10
base: v0.1.0
---
# 0.2.0

In one or two sentences: what went in and why to update.

## State of the games
A table: the game, the pointer, what is left (from a search over the HEAD of the game), what was really built.

## Steps
### 1. Title
Who: client / backend, which games.
Search: `git grep -nE '...' -- 'client/Assets/*.cs'`
Do: before -> after, a link to an example.

## Check the behaviour
What changed without a compile error and how to notice it.

## No action
What went in and needs nothing, one line each.
```

`base` is read by `status.sh`: the commit or the tag before the first commit of the release. If the pointer of a game is older than it, the
release file says what else to read. The search runs from the root of the game.
