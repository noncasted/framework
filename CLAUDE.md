# Framework

Shared base of the game projects (mines-leader, peace-data-reborn, pixel-swarm, ...): common reactive primitives,
client core, modules, platforms, forked Unity packages, shared protocol and backend infrastructure.

Every project consumes this repository as a git submodule at `<project>/tools/framework`:
- Unity: `client/Packages/manifest.json` -> `"file:../../tools/framework/client/..."`
- backend: `ProjectReference` -> `../tools/framework/backend/...` (relative to the csproj)
- shared, common: `ProjectReference` / UPM `file:` -> `tools/framework/shared`, `tools/framework/common`

## Layout

```
common/                 reactive primitives of both sides: lifetimes, event sources, lifetimed values and viewable
                        properties, viewable lists and dictionaries, ModifiableList (assembly Framework.Common, namespace
                        Internal, C# 9 and netstandard2.1, no Unity); Tests~/ holds its xunit tests
shared/                 protocol and types used by both the Unity client and the backend: Protocol/ (messages,
                        union tags), Session/ (the real-time session), Lobbies/ and Matchmaking/ (lobby rooms and the
                        session requests of the meta), ClientTelemetry/ (the telemetry of a client over its meta socket)
client/
  core/                 DI container (+ ContainerGenerator), scopes, content directory, catalogues, the Unity side of the reactive
                        primitives (GameObjectLifetime, UniTask waits, Lifetime Graph), editor tools
  flow/                 base of game loop, loaders and mocks (concrete mocks and the startup scene stay in the project)
  modules/              optional modules: publish, network, browser, audio, animations, cameras, inputs, backend, ui,
                        localization, settings, lobbies, telemetry, ...
  platforms/            platform implementations of modules/publish: web, website, itch, telegram, steam
  packages/             forks of third-party packages (render pipelines, uGUI, web stripping tool, UniTask, MemoryPack,
                        unity-mcp), see FORK.md in each; no Addressables since 0.7
  measurements/         the size of the WebGL build per Unity version: a template project, stripping variants
                        (base: the list of pixel-swarm; aggressive: no uGUI / TMP), tools/measure.py <version> generates
                        projects/<version>-<variant> (git-ignored), builds, measures, runs it headless; RESULTS.md, ANALYSIS.md
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
                        Game.Hosting and Game.Lobbies)
  ClientTelemetry/      telemetry of the clients: sessions and startup traces over the meta socket, storage, read API (namespace
                        Game.ClientTelemetry; rules in Contract/CONTRACT.md; the panel is in Console/Infrastructure/ClientTelemetry)
  ClientTelemetry.TestHost/  the same without a game: Kestrel, a silo, its own PostgreSQL container
.claude/docs/           docs of the patterns shared by the games, for Claude Code (INDEX.md: principles, keyword table)
.claude/skills/         skills shared by the games, linked from `.claude/skills` of each project
tools/scripts/          scripts shared by the projects; web-bench/: the headless stand and the devices of the web client
                        (skill device-lab)
releases/               releases: X.Y.Z.md with the steps a game takes to update, unreleased.md, README.md (format, versions)
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

## Docs

A game imports the index in its `CLAUDE.md`: `@tools/framework/.claude/docs/INDEX.md` from the root of the project,
`@../tools/framework/.claude/docs/INDEX.md` from `.claude/CLAUDE.md`. A nested `CLAUDE.md` like this one loads only when
files of its folder are touched; the import makes the index visible from the first message. It is loaded in every
session, so the index and the principles stay short and the other docs are read by keyword. A pattern true for every
game goes here, a pattern tied to the assets or rules of one game stays in that game.

@.claude/docs/INDEX.md

## Skills

A game links each shared skill it uses: `ln -s ../../tools/framework/.claude/skills/<name> .claude/skills/<name>` (one link
per skill: a folder of skills nested under the project would load late and under a prefixed name). The name of the skill
and its `.claude/skills/<name>/...` paths stay as they are; its own skills sit next to the links. Edit a skill at its target
here, the tools do not write through a link. No values of a game in a skill: `.claude/project-skills.md` (targets, urls,
look), `.claude/code-audit.json` (folders of the dependency check, namespaces of the game), `.claude/control.json` (cluster, editor), `.claude/deploy-client.json` (targets of the client deploy), `.claude/override-packages` (registry and
patches); `${CLAUDE_PROJECT_DIR}` for absolute paths.

oh-my-pi (`omp`) reads the same `.claude/skills`; its subagents live in `.omp/agents` (`audit-scout`, `audit-verifier` of the skill
`audit-swarm`, pinned to a cheap model) and its project limits in `.omp/config.yml`. A game links each agent:
`ln -s ../../tools/framework/.omp/agents/<name>.md .omp/agents/<name>.md`, and keeps its own `.omp/config.yml`.

## Extension points

- **Common.** `Framework.Common` is referenced directly by everything that uses a lifetime: every asmdef that
  references `Internal` (asmdef references are not transitive) and every backend csproj (no transitive project
  references). The waits are platform extensions: `ReactiveUniTaskExtensions` in the core (UniTask),
  `ReactiveTaskExtensions` in backend `Common` (Task). A listener of a lifetime or a handler of an event source that
  throws does not stop the others: the exceptions go to `ReactiveExceptions.Handler` after the pass (the client
  installs `Debug.LogException` in `ReactiveLogging`; the backend has no handler and gets the rethrow). The editor
  feeds the Lifetime Graph through `LifetimeTracker` (compiled only under `UNITY_EDITOR`). The sources must stay
  C# 9 and netstandard2.1: Unity compiles them as they are, and the dotnet build is pinned to the same.
- **Protocol.** `INetworkContext` is a plain interface; the project declares the union
  (`[MemoryPackUnionFormatter(typeof(INetworkContext))] partial class NetworkContextUnion`) with the framework tags
  (`NetworkContextTags`, 0-249) and its own (1000+), and registers it on start (backend: `NetworkProtocol.Register()`
  in the service defaults, Unity: on load). Members of type `INetworkContext` are `[MemoryPackAllowSerialize]`.
  The protocol hash belongs to the project (`ClientProtocolCheck.IsCurrent(context, hash)`). A project with real-time
  sessions lists the session tags too (`NetworkContextTags.Session*`, `Entity*`, `Object*`, `Player*`, `Service*`,
  `Latency*`, `Peer*`) and declares the unions over the payload interfaces of `shared/Session` the same way
  (`[MemoryPackUnionFormatter(typeof(IEntityPayload))] partial class EntityPayloadUnion`, also `IEventPayload`,
  `IStatePayload` when used), registered together with `NetworkContextUnion`.
- **Persisted type names.** States are stored with `$type`; `MovedTypesBinder` finds a type moved to another
  assembly by its full name, so keep namespaces when moving persisted types between the framework and a project.
- **Cluster.** Service roles are strings: the project declares its `ServiceTag`s and the required set
  (`ConfigureRequiredServices`); a game server calls `PublishGameServerUrl`. Game configs: `AddAddressableState` +
  an `ICoordinatorSetupCompleted` with `ConfigsInitializer.Init`. Game switches: `IClusterFeatures.SetGameFlag` /
  `IsGameFlagEnabled`.
- **Console.** A Razor class library: the project keeps its home page and game pages in its own assembly (add both
  assemblies to the router), sets role icons (`ServiceRoleIcons`), registers game flags (`AddConsoleGameFlag`) and
  extra infrastructure cards (`AddConsoleInfrastructureCard`, shown on `/infrastructure`), puts `HomeClusterSection` (Features,
  Client Telemetry) and `OnlineWidget` (online of the coordinator's `AddOnlineTracker`, page `/online`) on its home, and adds `tools/framework/backend/Console` to the
  Tailwind content paths. The `app.css` of a game is committed and built by hand, so no build of the framework refreshes it: a page
  of the framework uses only the classes that are in the `app.css` of every game.
- **Client telemetry.** `backend/ClientTelemetry`, off until the switch on the Features page of the console. The game calls
  `AddClientTelemetrySessions` on the silo, `AddClientTelemetry` on the meta gateway and `AddClientTelemetryReader` on the console (the
  panel appears from it, no other registration), maps `MapClientTelemetryRead().RequireConsoleToken()` and keeps the commands of its
  meta socket over `IClientTelemetryConnections.Of(session.Lifetime)`: `Start`, `Accept`, `End`, `AcceptStartup`; the id of the user
  stays in the game. A session holds the previous one its client did not close (`previous`, found back by `sessions?previousId=`);
  warnings are counted apart from the errors (`warningCount`). The test host runs without a game: `--token <value>` (required), `--seed`, `--console <folder>` (`theme.css` and
  `app.css` of a console of a game: its pages on this host), `--no-telemetry` (the console of a game without the telemetry).
- **Client core.** Assemblies `Internal`, `Internal.Editor`, `Internal.Tests` (namespace `Internal`, asmdef GUIDs of
  the projects kept; `Internal` is `GUID:f039cb8c565843dba6da5b3c1ccde7d6`, `Framework.Common`
  `GUID:3ea5c3f1172040daa713d5ad71728b78`, `Framework.Shared` `GUID:73681998e594465db2ed389d82d877b6`). The project owns the content assembly (`FrameworkSettings.ContentAssembly`, by default
  `Internal.Content` in `Assets/Common/Internal`): generated catalogues (`InternalAssets`, `Sprites`, audio, colors,
  `Scenes` of `ContentScene`s), catalogue groups and their json lists (one folder per kind, `Runtime/Catalogues/<Kind>`: the json
  list, the group assets and the generated classes together), the generated `ContentRoot` (`Runtime/Catalogues/ContentRoot.asset`),
  the `OptionsContainer` asset, the container asset map
  (`ContainerGraph.AssetMap.cs`), the startup preload and the root scope (`InternalScopeExtensions.Construct`, loaded
  with `RootScopeExtensions.LoadRoot`). Every project asmdef that references `Internal` references the content
  assembly too. Keys of the game's local storage live in the game (`LocalStorageKeys.Get(name)`).
- **Project layout for the editor tools.** `ProjectSettings/Framework.json` (`FrameworkSettings`, required: no game
  defaults, `ContentAssembly` and `ContentFolder` must be set): content folder and
  assembly, module roots, scene folders, art and audio folders, favorite scenes, player assemblies to strip, the content
  directory (`Content`: `Name`, `Roots`, `IsPreloaded`; required by a player build only). A game without the settings gets
  the error. The live graphs write into `<ContentFolder>/Editor/Transient`: the project ignores `Transient*`
  there in git. Editor assets of the framework are loaded from `FrameworkSettings.Package(...)`.
- **Screen canvases.** The canvases the framework builds from code (the latency overlay of `network`, the outdated
  client notice of `ui`) go where the game says in its root scope (`IScreenCanvasCamera` of the core): `AddScreenCanvasCamera(camera,
  layer, planeDistance)` (Screen Space - Camera: the whole canvas on the layer, the camera asked on every show) or, explicitly,
  `AddScreenCanvasOverlay()`. Without one the scope that builds them does not resolve. The Overlay mode lost its projection on
  iPadOS 18.2 Safari; the camera and its drawing stay in the game.
- **Content.** One content directory per game (`CONTENT.md`) instead of Addressables (removed in 0.7): `ContentBuild` builds it
  with every player build (an `IPlayerBuildStep` and a `BuildPlayerProcessor` for the build window; `IContentBuildStep` runs first,
  the `ContentRoot` generator among them) into `Library/Content/<name>`, checks its lazy references (empty or outside it fail the
  build), bakes its index into `Resources` and ships it as `StreamingAssets/Content/<name>` (`IsPreloaded` on WebGL: inside the
  `.data`; a WebGL build with brotli ships `<file>.br`, which the server sends with `Content-Encoding: br` as `Build/*.br`). The root scope calls `await builder.AddContent(ContentOptions)` once (`IContentLoader`; binds the catalogues to the
  roots; its lifetime keeps the directory registered); scopes `LoadContent(reference)` / `RequestContent(reference, out slot)`
  (container primitives), scenes `FindOrLoadSceneWithServices(ContentScene)`, `WithAssetScene(ContentScene)`; during the game
  `IContentLoader.Load / LoadScene / Fetch / Progress` on the lifetime of the owner, which owns the result (counted per
  reference instance, one fetch per file). References: `ContentReference<T>` (`Loadable<T>`), `ContentScene`
  (`LoadableSceneId`), only inside assets of the directory (a player scene erases them). Stores: local (`StreamingAssets`),
  web (zeros + fetch, Cache API `content`, `remoteUrl` for a CDN), Play Mode without a directory (assets of the project).
  `IsPreloaded` groups (`preloaded` in the json lists) are read synchronously. Unity 7000 alpha API.
- **Player build steps.** A project step around the build entry points of the platforms implements `IPlayerBuildStep`
  (`core/Editor/Tools/Build`: `BeforeBuild` / `AfterBuild(target, succeeded)`, a parameterless class, found by
  `TypeCache`, ordered by full type name); `PlayerBuildSteps.Run(target, build)` calls `AfterBuild` of every started
  step also after a failed build or a throwing step, then rethrows. Unity's postprocess callbacks skip failed builds.
- **Test tools.** `Internal.TestTools` (`core/Tests/Tools`, Editor, `UNITY_INCLUDE_TESTS`; a game references it from its test asmdef
  and lists `com.noncasted.framework.core` in `testables`): `FrameAllocations.AssertNone(what, passes, pass, warmup)` / `Measure`, the
  guard of a hot path (below a byte a pass); `WebGlShaderCheck.Find(folders, excluded)` + `AssertSafe(shaders)`, the WebGL code of the
  shaders without `ImmCB_` / `TempArray` / flat varyings (`WEBGL_TROUBLESHOOTING.md` G1, G2); the folders are the game's, in its test.
- **Startup trace.** `GameProfiler.Stage` opens a big step of the game (`Global`, `Meta`, a menu, a run), a root of the
  trace; `LoadRoot` opens `Startup`. A record made with an empty stack (after an await) goes to the last stage, `Detached` to
  the last stage off the stack, and a parent covers its late children. A branch that outlives its stage opens `Detached` in its
  synchronous part, hangs the later steps on that span and creates what they measure inside `Ambient(span)`. The loads of content
  after the start go through `AssetLoads` (`Started`, `Ended`, `Running`): every load of `IContentLoader` (`Content: <name>`), so every `AssetGroup`
  and every scene of `ServiceScopeSceneLoader`; a game that loads content in another way wraps the load in `AssetLoads.Begin(key)` ... `Finish()` / `Fail(exception)`.
- **ContainerGenerator.** Sources in `client/core/Source~`, the dll in `client/core/Analyzers` (`RoslynAnalyzer`
  label, every platform off). After a change: build it (it copies itself into `Analyzers`) and run
  `ContainerGenerator.Verify`.
- **Updater.** The global scope calls `AddUpdater(prefab)` (core); delays without coroutines:
  `updater.RunDelay(lifetime, time)`; every updater wait (RunDelay, RunUpdateAction, Progression, Tween) ends with
  `OperationCanceledException` when its lifetime ends first. `Pause` / `Continue` are counted (Continue without Pause
  throws); an updatable that throws does not stop the others (reported through `ReactiveExceptions`).
- **Hierarchy bindings.** Generated `*Bindings.g.cs` (`ObjectBindings`) go to `Assets/Generated` of a module root
  (`FrameworkSettings.ModuleRoots`), to `Generated` next to any other asmdef, to
  `Runtime/Tools/PrefabHierarchy/Generated` of the content folder for an owner from the content assembly; an asset
  outside every asmdef is an error. One object may carry several bindings classes: a prefab variant keeps the ones
  of its base and mirrors only what it added (`_layerAssetGuid`). Prefab batch:
  `HierarchyBindingsBatch.RegeneratePrefabs`, then `HierarchyBindingsGenerator.PumpPending()` until `Idle.` when the
  editor is not focused; check with `HierarchyBindingsValidator`.
- **Flow.** The project's startup derives from `StartupEntry`, its mocks from `MockEntry` (`MockSwitcher` runs the mock
  of a scene opened directly); `GameLoopScopeLoader` keeps one game loop scope at a time.
- **`[Inject]` methods** of a type that may be registered from another assembly are `public`: the generator does
  not see an `internal` method of a foreign assembly and does not call it (diagnostic CINGR010).
- **Modules.** Assemblies `Internal.<Module>`, namespaces `Global.<Module>` (the global scope of the game registers
  them). Each has an `Add*` extension of `IScopeBuilder`; prefabs and options come as arguments (the project owns
  the assets and its generated `GlobalPrefabs` / `GlobalAssets`), so the project's global `Construct` calls them:
  - `animations` (`Internal.Animations`, namespace `Internal`, no `Add*`): sprite animations of entities on the
    updater (`ForwardSpriteAnimation`, `RotatableSpriteAnimation`, `RegisterSpriteForwardAnimation`), the
    `SpriteAnimationRenderer` component. `ISpriteAnimationData` stays in core (the sprite catalogue returns it).
  - `backend` (`AddBackend`): HTTP get / post / media.
  - `cameras` (`AddCamera(GlobalCamera prefab)`): camera of the global scope, `ICurrentCamera` (the camera of the
    scene set by the game: `Current`, the reactive `Value`, `SetOutputResolution` for the screen and the render texture
    of every camera set later), `ICameraUtils` (`ScreenToWorld` takes a perspective camera's point on its near plane).
  - `audio` (`AddAudio(player, listener)`): pooled sounds with limits, crossfaded music on two sources. The scope
    must have an `IAudioVolume` (the settings of the game implement it). Button sounds are wired by the game. Sounds
    go on lines (`AudioLine.Music` / `SFX`, lines of the game with `AudioLine.Of<TVolume>`), may be trimmed
    (`SoundPlay.WithTrim`), clips loaded at runtime play as `clip.ToSound()`, music may be bound to a lifetime.
  - `inputs` (`AddInput(InputOptions, EventSystem prefab)`): input scheme, gamepad by meaning, back / pause.
    Controls of a game (aim, sticks, touch layout) are options of the game. `InputManager.asset` of the project must
    hold the axes `Gamepad Axis 1..10` (type: joystick axis, axis N-1): `GamepadReader` throws at build without them.
    A platform without a gamepad layout (macOS, Android, iOS) gets `NoGamepadReader`.
  - `ui` (`AddUI`, `AddLoadingScreen(prefab)`, `AddScreenSafeArea`, `AddClientOutdatedNotice(style)`): UI state
    machine and input constraints, design elements and layouts (+ editors), gamepad navigation (the event system
    prefab needs a `StandaloneInputModule`), safe area, loading screen, the notice of an outdated client
    (`IClientOutdatedNotice` of the core, shown by the network on close code 4001; built from code, the game gives
    the font, texts and colors in `ClientOutdatedNoticeStyle`; needs `IUINavigation`, so `AddUINavigation` too). `AddUI` = `AddUIStateMachine` + `AddUINavigation`: a game on `InputSystemUIInputModule` without a gamepad
    registers `AddUIStateMachine` only. Kinds of input constraints are instances of `InputConstraints` (`Game` in the
    framework, the project declares its own). Depends on `inputs`.
  - `publish`: platform abstractions (`ISaves`, `IPlatformAuth`, back button, inventory, invites, achievements). The project
    composes its publisher from the platforms; registrations under a runtime `if` around another installer are all
    built by the generator, so a platform choice is made with `#if` or inside one installer.
  - `browser` (`Internal.Browser`): fullscreen, leave confirmation and clipboard of a web page.
  - `network` (`Internal.Network`, namespace `Internal`): see Network client below.
  - `localization` (`AddLocalization(LocalizationOptions)`): texts in several languages from the table asset of the
    game (`Localization.json`, a column per `Language`), `TextKey` declared in code, `LocalizedText` with format
    arguments, the static `Localizer` (current language, `FromSystem`, `Next`), `LocalizedLabel` keeps a TMP text in
    the current language. Depends on TextMeshPro.
  - `settings` (`AddSettings(SettingsOptions)`, `AddSettings<TSettings, TSave, TService>(SettingsOptions<TSave>)`):
    the settings of the player in the saves (`ISaves`): the volumes as the `IAudioVolume` of `audio`, the defaults from the options asset of the game; a game with settings of its own derives
    `SettingsSave`, `Settings<TSave>` (`OnPush`) and `SettingsOptions<TSave>` and injects its own interface over
    `ISettings<TSave>`. A language is a field of the save of the game (`OnLoad`, `OnPush`). Depends on `audio`, `publish`.
  - `resolution` (`AddResolution()`, namespace `Global.Resolution`): the render resolution of a phone or a tablet on the web,
    Auto / Low / High (`ResolutionMode`; Auto is Low on iOS and iPadOS). The settings of the game implement `IResolutionSetting`.
    The build puts `WebTemplate~/render-resolution.js` first into the `<head>` (`RenderResolutionInjection`); the template calls
    `renderResolution.pixelRatio(canvas, max)` before `createUnityInstance` and `renderResolution.follow(module, canvas, max)` on a
    mobile page (device / k with k a whole number, the short side at least 360, `?dpr=native|<n>`). `AddResolutionTelemetry` in the
    scope of the telemetry: `settings.resolution`.
  - `framerate` (`AddFrameRate(AutoFrameRateOptions)`, after `AddUpdater`, namespace `Global.FrameRate`): `Application.targetFrameRate`,
    strongest first: `?fps=native|<n>` of the page, the choice of the settings (`IFrameRateSetting`, Auto / 30 / 60), Auto: the rate
    of the screen on a desktop, on a phone `AutoFrameRateRule` (work of a frame above 12 ms for 8 s -> 30, below 6 ms for 30 s -> 60),
    remembered on the device (`FrameRate.AutoLow`). `AddFrameRateTelemetry`: the marks `fps.auto.<limit>`. The `*.Telemetry` assemblies
    of both compile only with the `telemetry` package (`FRAMEWORK_TELEMETRY`, a version define; `client/project` sets it).
  - `lobbies` (`Internal.Lobbies`, namespace `Global.Lobbies`; `AddMatchmaking`, `AddLobbyNetworkService`):
    `IMatchmaking` (search, solo, lobby create / join / match join, tickets) over the meta socket the game registers
    as `IMetaSocket`; `LobbyNetworkService`, the client of the lobby service of a lobby session (a derived one adds the
    properties of the game in `CollectProperties`); `OfflineLobbyService` for the offline local server (the game
    gives `IOfflineLobbyMatches`); `SessionData`, `LobbyJoinResult`, `SessionSearchResult`. Depends on `network`.
  - `telemetry` (`Internal.Telemetry`, namespace `Global.Telemetry`; `AddSocketClientTelemetry(options)`,
    `AddStandardTelemetryCollectors`): metrics, errors and the startup trace over the meta socket, which the game gives as
    `ITelemetrySocket`, so it is called in the scope of the meta backend. `IClientTelemetry`: `StartSession(lifetime, tags)` per part of the
    game, `Mark`, `SendStartupTrace` after every authorization of the socket. Nothing runs without a session; a game adds a
    collector as `ITelemetryCollector` in the scope of the telemetry, or from a scope below (a run) with
    `AddCollector(lifetime, collector)`: sampled in every session while the lifetime lives, a live one started per session with the
    session cut by its lifetime. The standard collectors also time the frame: markers in the `PlayerLoop` for a session only
    (`frame.phase.*`, `frame.fixed_steps`, `frame.spikes` and the marks `frame.spike` / `frame.spike.<phase>` from 250 ms, `gc.allocated`;
    under 1 us per frame in the editor), `render.width` / `height` / `pixels` of the drawing buffer, `settings.*`, and on WebGL
    `frame.gpu.query` (`EXT_disjoint_timer_query_webgl2`, one frame in 30), `render.canvas_*` / `dpr`, `web.raf.dt.*` (an own chain of
    `requestAnimationFrame`), `web.gl.*` numbers of the context; `AddSocketClientTelemetry` adds `net.meta.rtt` / `bytes_*`. On WebGL `AddClientTelemetry` also listens to the context of the canvas (`Module.canvas`, from the build of the
    scope, nothing prevented): the metric `web.gl.context_lost`, the marks `webgl.context_lost` / `webgl.context_restored` and the error
    `WebGL context lost`; a loss outside a session waits on the page and comes once with the next one. Unity stops its player loop while the
    context is lost, so all this is sent only after a restore; without one the samples just stop and the session closes as `lost`. Also on WebGL:
    `web.gl.errors` (`gl.getError` once a sample, its time `web.gl.errors_ms`) with the marks `webgl.error.<code>` and an error per code; the errors emscripten raises itself in its gl* functions
    (`GL.recordError`, never seen by `getError`) apart: `web.gl.emscripten_errors`, the marks `webgl.emscripten.<code>` with the function and
    an error `... in <glCall> (emscripten)`; a page opened with `?glcheck` checks every call of the context (`getError` after it, slow, nothing
    wrapped without the parameter): `web.gl.check_errors`, the first error of each code and call as the mark `webgl.check.<code>` with
    `call(arguments)`, the frame and the time of the page and an error by the code and the call, its codes still counted once a sample in `web.gl.errors`;
    the calls of `GLctx`, wrapped once per context: `web.gl.shader_compiles` / `shader_compile_ms` / `program_links` / `program_link_ms`
    (the queries of the status in the time), `web.gl.buffer_bytes` / `buffer_data_calls` / `texture_bytes` / `texture_uploads`, the mark
    `webgl.shader.slow` (`slowShaderMs`); the marks of the page (`page.hidden` / `visible` / `pagehide[.persisted]` / `pageshow.persisted` /
    `freeze` / `resume` / `fullscreen.enter` / `fullscreen.exit`, `page.resize` with the sizes, collapsed) and `audio.context.<state>` of
    the `AudioContext` of Unity. A development build adds `dev.*` (counters it alone feeds, the markers of `devProfilerMarkers`). Everywhere: `memory.texture_count`,
    the warnings of the log of Unity as the level `Warning` under their own limits, and the trace of the open session (local storage, a
    file outside the web, with the main numbers of its last 5 samples) that the next start of the same browser reports as
    `SessionStart.Previous` when the page died inside it.
    The loads of assets in a session are marks `load.start` / `end` / `failed` / `slow` / `unfinished` with the key and the duration
    (`AssetLoads` of the core; `slowLoadSeconds` of the options). On the web the startup trace begins at the origin of the page: the
    root `Page` (navigation, files with sizes and cache, the gap to the build, the spans of the template); the build puts
    `WebTemplate~/startup-trace.js` (`window.startupTrace.begin/end/mark`) first into the `<head>` of the built `index.html`
    (`TelemetryStartupTraceInjection`), the template of the game only calls it. Depends on `network`.
- **Platforms.** `itch` (`AddItchSaves`: saves in PlayerPrefs), `web` (`AddWebLoadingScreen`, `AddWebSafeArea`,
  `WebShell.UseMobileCheck`, which also turns Run In Background off on a mobile page; needs `window.loadingScreen` and, for the shell, `TemplateData/web-shell.js` in the
  WebGL template; register only under `#if UNITY_WEBGL && !UNITY_EDITOR`; editor: `Internal.WebGlBuild.Build`,
  the batchmode build with the active build profile inside `PlayerBuildSteps.Run`, exit code 1 on any failure; every WebGL
  build gets `WebTemplate~/audio-session.js` first in the `<head>` of its `index.html` (`WebAudioSessionInjection`:
  `navigator.audioSession.type = 'playback'`, so the silent mode of iOS does not mute the sound; `BuiltPageScripts` of the core
  puts such scripts, the startup trace of `telemetry` too), `steam` (`AddSteam(SteamOptions, PlatformOptions)`, the `NoPlatform*` variants of `publish` off Steam: app id, Web API identity
  of the backend auth ticket, the connect key of invites; `#if UNITY_STANDALONE`, needs Steamworks.NET), `telegram`
  (`AddTelegramAPI(PlatformOptions, TelegramEditorOptions)`: the bridge to `window.telegramShell` of the Telegram WebGL template, a debug one in the editor that signs the init data with `TelegramBotToken` of the local backend settings file the game names in `TelegramEditorOptions`; auth and back button are alternatives the game picks).
- **Sessions.** Shared types in `shared/Session` (auth, entities, objects, players, services, latency, entity ids:
  `SharedEntityIds`, range 0 issued by the backend). Backend `backend/Session`: `AddSessionServices(data, options)`
  builds the container of one session; the game adds its network services (`IService`), commands and logic and
  registers an `ILoggerFactory`. `SessionCreateOptions`: type, expected users (bots included), places (`MaxUsers`,
  bots take none), `JoinTimeout`, `ReconnectWindow` (zero - a lost connection is a leave; otherwise the seat waits
  and the others get `RemoteAway`). `IUser.Id` is the id of the user in this session (sent to the clients),
  `IUser.BackendId` the meta id (never sent). Bots: `IUserFactory.CreateBot`. Entities built by the backend:
  `IEntityFactory.Create(seat)`. Users and seats are created in the execution queue of the session only. The factory
  of the sessions, the gateway and the search stay in the game.
- **Hosting.** `backend/Hosting`. A game server calls `AddSessionHosting(LobbyOptions)`: the sessions collection
  (`ISessionsCollection`, feeds `LiveSessionsData`), the lobby sessions (`ILobbySessions`: create, join by a code,
  the lobby of the late players of a running match), the leases of the lobby codes (`ILobbyCodeLeases`,
  `ILobbyListings`) and the handlers of the meta requests (`SessionEndpoints`, `SessionPayloads` over the message
  pipes). The game registers what builds its sessions: `ILobbyFactory` (a lobby session from `LobbyCreation`),
  `ILobbyMatches` (the match a lobby starts, the admission of a late player), `IKnownUsersMatches` (a solo match),
  `ISessionSearch` (the public match of the search) and `ILobbyEntryPolicy` (when a running match lets a late player
  in; `LobbyEntryPolicy.Always` when always). The container of a lobby session adds `AddLobbyService(LobbySessionInfo)`
  (or `AddLobbyService<T>` with a derived service: own properties from `SharedLobbyService.GamePropertyFirst`, the
  admission of the late players in `OnLatePlayersStarted` / `Admit` / `ClearGame` / `MarkRunOver`), the container of
  a match started by a lobby `AddMatchListingService(MatchListingInfo)`. The meta calls `AddSessionRequester`:
  `ISessionRequester` (search, solo, lobby create / join / match join, tickets) and `IOpenLobbiesWatcher`; the
  commands of the meta socket that call them stay in the game. The lobby codes live in the registry grain of the
  deploy (`backend/Cluster/Lobbies`, `ILobbyCodes`). The protocol: `SharedSessionRequests` (`Search`, `CreateSolo`,
  `Result`, `TicketRequest`) and `SharedLobbies` (`CreateLobby`, `JoinLobby`, `JoinMatch`, `Subscribe`, `List`),
  tags `NetworkContextTags.Session*` / `Lobb*` (150-155, 180-184), listed by the project in its union.
- **Network client.** Module `network` (`Internal.Network`, namespace `Internal`): the root scope calls
  `AddNetwork(NetworkOptions, ProtocolHash.Value)` (the options asset belongs to the project), connections come
  with `AddNetworkConnection` / `AddSessionServices`. Entities of the local user with a backend id come to
  `ListenRemote`, the ones its client created before a reconnect to `ListenRestored`. Also the offline local server
  (`offline://` urls, `ILocalServers`), latency monitor and watchdog (the backend answers `Ping`), and
  `RemoteStateMachine`. A request is a span of the running trace unless its message is `IUnprofiledRequest` (what repeats through the
  whole game: the telemetry).
- **Package versions.** `Directory.Packages.props` here; the project's `Directory.Packages.props` imports it.

## Tests

`tools/scripts/test-all.sh` runs the whole suite on the current tree, uncommitted edits included: `dotnet build
framework.slnx`, then backend, common and `ContainerGenerator.Verify`; next to them `control` and `telemetry` (python, the tests of these skills), Unity EditMode (`client/project`, needs
its `Library`) and PlayMode (second copy in `.build/test-all/playmode`, kept between runs). It prints a table stage / time /
tests / result, the failed tests and the logs (`.build/test-all/logs`); any failure gives a non-zero code. It leaves
`Analyzers/ContainerGenerator.dll` alone: after a change of the generator the Unity stages use the committed dll until a
Release build replaces it. The expected numbers: backend 1565, common 96, Verify 171, control 23, telemetry 84, EditMode 1237 (4 of them skipped, the
benchmarks of `ContainerBenchmarkTests`), PlayMode 139 (24 of them skipped, the end to end tests of the telemetry without their host);
the script does not check them, a smaller number is a lost test.

`tools/scripts/test-telemetry-transport.sh` runs those 24 against a real backend: it starts `ClientTelemetry.TestHost` (needs Docker) and takes
`client/project`, so no other editor may have it open.

`tools/scripts/test-telemetry-transport.sh` runs apart from the suite: the real client of `modules/telemetry` in PlayMode over a real
websocket against `backend/ClientTelemetry.TestHost` (Kestrel, a silo, its own PostgreSQL container, the real switch), started and
stopped by the script. Without the host the tests `TelemetryTransportEndToEndTests` are ignored, so `test-all.sh` counts them as
skipped; their union over `INetworkContext` is dynamic and registered only with the host, for their namespace, so without it they change nothing for `network` or for the union of a game. The host alone, with sessions to read (the sockets `/test-host/meta`, and `/test-host/mute` that answers nothing, without a token; `/test-host/flag/{bool}` and `/test-host/stats` next to the API under the
token): `.build/bin/ClientTelemetry.TestHost/Debug/net10.0/Framework.ClientTelemetry.TestHost --token <t> --port <n> --seed`; a session open on a quiet socket closes as `lost` after `--lost-after <seconds>`, 15 here. `--startup-file <path>` (with `--seed`) sends a file of the Startup Profiler as the startup trace of the seeded sessions.

One suite by hand: `.build/bin/Tests/Debug/net10.0/Framework.Tests` (backend),
`.build/bin/Framework.Common.Tests/Debug/net10.0/Framework.Common.Tests` (common): xunit v3 on the Microsoft Testing
Platform, run the executable, `dotnet test` refuses on the .NET 10 SDK. The projects keep their own tests of the framework
code until they move here.

## Working from a project

The submodule is a normal git repository: commit and push inside `tools/framework`, then commit the new submodule
pointer in the project. Other projects update through the `update-framework` skill: it moves the pointer to a release and
carries out the steps of `releases/<version>.md`. A release is cut by the `release-framework` skill.
