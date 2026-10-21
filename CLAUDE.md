# Framework

Shared base of the game projects (mines-leader, peace-data-reborn, storytale-workshop, ...): client core, modules,
platforms, forked Unity packages, shared protocol and backend infrastructure.

Every project consumes this repository as a git submodule at `<project>/tools/framework`:
- Unity: `client/Packages/manifest.json` -> `"file:../../tools/framework/client/..."`
- backend: `ProjectReference` -> `../tools/framework/backend/...` (relative to the csproj)
- shared: `ProjectReference` / UPM `file:` -> `tools/framework/shared`

## Layout

```
shared/                 protocol and types used by both the Unity client and the backend
client/
  core/                 DI container (+ ContainerGenerator), reactive, lifetimes, scopes, catalogues, editor tools
  flow/                 base of game loop, loaders and mocks (concrete mocks and the startup scene stay in the project)
  modules/              optional modules: publish, network, browser, audio, cameras, inputs, backend, ui, ...
  platforms/            platform implementations of modules/publish: web, website, itch, telegram, steam
  packages/             forks of third-party packages (Unity, UniTask, MemoryPack, unity-mcp), see FORK.md in each
backend/
  Common/ Infrastructure/ Cluster/ Console/
  Session/              real-time game sessions (assembly Framework.Session, namespace Game.Session)
tools/scripts/          scripts shared by the projects
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
7. **Fail fast.** No silent fallbacks, no defensive null checks; unexpected state throws.
8. **A change here affects every project.** After a change, build at least the project you are working in; mention in
   the commit message when a change is breaking (`BREAKING:`).

## Extension points

- **Protocol.** `INetworkContext` is a plain interface; the project declares the union
  (`[MemoryPackUnionFormatter(typeof(INetworkContext))] partial class NetworkContextUnion`) with the framework tags
  (`NetworkContextTags`, 0-249) and its own (1000+), and registers it on start (backend: `NetworkProtocol.Register()`
  in the service defaults, Unity: on load). Members of type `INetworkContext` are `[MemoryPackAllowSerialize]`.
  The protocol hash belongs to the project (`ClientProtocolCheck.IsCurrent(context, hash)`). A project with real-time
  sessions lists the session tags too (`NetworkContextTags.Session*`, `Entity*`, `Object*`, `Player*`, `Service*`,
  `Latency*`) and declares the unions over the payload interfaces of `shared/Session` the same way
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
  extra infrastructure cards (`AddConsoleInfrastructureCard`), and adds `tools/framework/backend/Console` to the
  Tailwind content paths.
- **Client core.** Assemblies `Internal`, `Internal.Editor`, `Internal.Tests` (namespace `Internal`, asmdef GUIDs of
  the projects kept). The project owns the content assembly (`FrameworkSettings.ContentAssembly`, by default
  `Internal.Content` in `Assets/Common/Internal`): generated catalogues (`InternalAssets`, `Sprites`, audio, colors,
  `Scenes`), catalogue groups and their json lists (one folder per kind, `Runtime/Catalogues/<Kind>`: the json list,
  the group assets and the generated classes together), the `OptionsContainer` asset, the container asset map
  (`ContainerGraph.AssetMap.cs`), the startup preload and the root scope (`InternalScopeExtensions.Construct`, loaded
  with `RootScopeExtensions.LoadRoot`). Every project asmdef that references `Internal` references the content
  assembly too. Keys of the game's local storage live in the game (`LocalStorageKeys.Get(name)`).
- **Project layout for the editor tools.** `ProjectSettings/Framework.json` (`FrameworkSettings`): content folder and
  assembly, module roots, scene folders, art and audio folders, favorite scenes, phases of the shared addressable
  groups, player assemblies to strip. Editor assets of the framework are loaded from `FrameworkSettings.Package(...)`.
- **ContainerGenerator.** Sources in `client/core/Source~`, the dll in `client/core/Analyzers` (`RoslynAnalyzer`
  label, every platform off). After a change: build it (it copies itself into `Analyzers`) and run
  `ContainerGenerator.Verify`.
- **Updater.** The global scope calls `AddUpdater(prefab)` (core); delays without coroutines:
  `updater.RunDelay(lifetime, time)`, ends earlier with the lifetime.
- **Flow.** The project's startup derives from `StartupEntry`, its mocks from `MockEntry` (`MockSwitcher` runs the mock
  of a scene opened directly); `GameLoopScopeLoader` keeps one game loop scope at a time.
- **`[Inject]` methods** of a type that may be registered from another assembly are `public`: the generator does
  not see an `internal` method of a foreign assembly and silently does not call it (no diagnostic). Check with
  `-p:EmitCompilerGeneratedFiles=true` and a grep for `.Construct(` in `*ConstructContainer.g.cs`.
- **Modules.** Assemblies `Internal.<Module>`, namespaces `Global.<Module>` (the global scope of the game registers
  them). Each has an `Add*` extension of `IScopeBuilder`; prefabs and options come as arguments (the project owns
  the assets and its generated `GlobalPrefabs` / `GlobalAssets`), so the project's global `Construct` calls them:
  - `backend` (`AddBackend`): HTTP get / post / media.
  - `cameras` (`AddCamera(GlobalCamera prefab)`): camera of the global scope, `ICurrentCamera` (the camera of the
    scene set by the game), `ICameraUtils`.
  - `audio` (`AddAudio(player, listener)`): pooled sounds with limits, crossfaded music on two sources. The scope
    must have an `IAudioVolume` (the settings of the game implement it). Button sounds are wired by the game.
  - `inputs` (`AddInput(InputOptions, EventSystem prefab)`): input scheme, gamepad by meaning, back / pause.
    Controls of a game (aim, sticks, touch layout) are options of the game.
  - `ui` (`AddUI`, `AddLoadingScreen(prefab)`, `AddScreenSafeArea`, `AddClientOutdatedNotice(style)`): UI state
    machine and input constraints, design elements and layouts (+ editors), gamepad navigation (the event system
    prefab needs a `StandaloneInputModule`), safe area, loading screen, the notice of an outdated client
    (`IClientOutdatedNotice` of the core, shown by the network on close code 4001; built from code, the game gives
    the font, texts and colors in `ClientOutdatedNoticeStyle`). Depends on `inputs`.
  - `publish`: platform abstractions (`ISaves`, `IPlatformAuth`, back button, inventory, invites). The project
    composes its publisher from the platforms; registrations under a runtime `if` around another installer are all
    built by the generator, so a platform choice is made with `#if` or inside one installer.
  - `browser` (`Internal.Browser`): fullscreen, leave confirmation, reload and clipboard of a web page.
  - `network` (`Internal.Network`, namespace `Internal`): see Network client below.
- **Platforms.** `itch` (`AddItchSaves`: saves in PlayerPrefs), `web` (`AddWebLoadingScreen`, `AddWebSafeArea`,
  `WebShell.UseMobileCheck`; needs `window.loadingScreen` and, for the shell, `TemplateData/web-shell.js` in the
  WebGL template; register only under `#if UNITY_WEBGL && !UNITY_EDITOR`; editor: `Internal.WebGlBuild.Build`,
  the batchmode build with the active build profile), `steam` (`AddSteam(SteamOptions)`: app id, Web API identity
  of the backend auth ticket, the connect key of invites; `#if UNITY_STANDALONE`, needs Steamworks.NET), `telegram`
  (`AddTelegramAPI(PlatformOptions)`: the bridge to `window.telegramShell` of the Telegram WebGL template, a debug one in the editor; auth and back button are alternatives the game picks).
- **Sessions.** Shared types in `shared/Session` (auth, entities, objects, players, services, latency, entity ids:
  `SharedEntityIds`, range 0 issued by the backend). Backend `backend/Session`: `AddSessionServices(data, options)`
  builds the container of one session; the game adds its network services (`IService`), commands and logic and
  registers an `ILoggerFactory`. `SessionCreateOptions`: type, expected users (bots included), places (`MaxUsers`,
  bots take none), `JoinTimeout`, `ReconnectWindow` (zero - a lost connection is a leave; otherwise the seat waits
  and the others get `RemoteAway`). `IUser.Id` is the id of the user in this session (sent to the clients),
  `IUser.BackendId` the meta id (never sent). Bots: `IUserFactory.CreateBot`. Entities built by the backend:
  `IEntityFactory.Create(seat)`. Users and seats are created in the execution queue of the session only. Hosting
  (factory, gateway, search, lobbies) stays in the game.
- **Network client.** Module `network` (`Internal.Network`, namespace `Internal`): the root scope calls
  `AddNetwork(NetworkOptions, ProtocolHash.Value)` (the options asset belongs to the project), connections come
  with `AddNetworkConnection` / `AddSessionServices`. Entities of the local user with a backend id come to
  `ListenRemote`, the ones its client created before a reconnect to `ListenRestored`. Also the offline local server
  (`offline://` urls, `ILocalServers`), latency monitor and watchdog (the backend answers `Ping`), and
  `RemoteStateMachine`.
- **Package versions.** `Directory.Packages.props` here; the project's `Directory.Packages.props` imports it.

## Tests

`dotnet build framework.slnx`, then `.build/bin/Tests/Debug/net10.0/Framework.Tests` (xunit v3, Microsoft Testing
Platform: run the executable, `dotnet test` refuses on the .NET 10 SDK). The projects keep their own tests of the
framework code until they move here.

## Working from a project

The submodule is a normal git repository: commit and push inside `tools/framework`, then commit the new submodule
pointer in the project. Other projects pick the change up with `git submodule update --remote tools/framework`.
