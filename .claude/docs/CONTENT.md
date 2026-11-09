# Content directories

**TL;DR:** since 0.7 the assets a game loads after the start (catalogue groups, scenes of scopes, lazy references) live in one
Unity content directory (`BuildPipeline.BuildContentDirectory`, Unity 7000 `Loadable<T>` / `LoadableSceneId`), built with every
player build. Addressables are gone from the framework. Code: `client/core/Runtime/Content`, editor `client/core/Editor/Content`
(namespace `Internal`). The rules of the API are those of the updater waits: a lifetime first, the lifetime owns the result.

## Model

- **One directory per game.** Its name and roots are the `Content` section of `ProjectSettings/Framework.json`:

  ```json
  "Content": { "Name": "Game", "Roots": [ "Assets/Common/Internal/Runtime/Catalogues/ContentRoot.asset" ], "IsPreloaded": false }
  ```

  A root is an asset registered with the directory; the build takes the roots and everything they reach.
- **`ContentRoot` is generated** (`<ContentFolder>/Runtime/Catalogues/ContentRoot.asset`, Codegen/Content Root, a step of Regenerate
  All and of every content build): lazy references to the catalogue groups (`_groups`, `_preloadedGroups`) and the scenes of the scene
  folders (`_scenes`). It holds no direct reference: the registration opens the files of the roots and of what they reference
  directly, so the root stays thin. Never edit it by hand.
- **Lazy references.** `ContentReference<T>` (an asset, wraps `Loadable<T>`) and `ContentScene` (wraps `LoadableSceneId`), each with
  the GUID / local id of the target written by the editor (the engine has no public runtime API to read them from the id). The owner
  of a reference loads without its target. They work only inside assets of the directory: the player build erases a `Loadable` in a
  scene of the player (`Startup`, Build Settings) to `{ Invalid }`.
- **The lifetime owns the result.** An object is released, a scene unloaded when the lifetime of its load ends. A lifetime that ends
  during the load gives `OperationCanceledException`; a load the engine started goes on and is released at once; a download no other
  load waits for is aborted (`AbortController`).
- **Counted per reference instance.** Two loads of the same `ContentReference` share one engine load, released when the last lifetime
  ends; two references to one target are two `Loadable`s the engine counts itself and gives one object. One download per file: two
  loads that need one file wait for one fetch.
- **Zeros + fetch.** A fetched store writes the manifest and the files of the roots, and zeros of the real size for every other file;
  the engine lists the folder once at the registration and never reads a lazy target before its load, so a load first fetches its
  real files over the zeros.
- **The directory is never unregistered while the game runs.** `UnregisterContentDirectory` destroys every object loaded from it,
  and a second registration of the same folder gives new instances: the directory lives as long as the root scope.

## API

Root scope, once, before the first load (`ContentOptions` is a field of the options of the game: `_name` equal to `Content.Name`,
`_remoteUrl` for a CDN, empty = `StreamingAssets` of the page):

```csharp
await builder.AddContent(options.Content);   // IContentLoader, CatalogContent bound, preloaded groups fetched
```

Scopes:

```csharp
var encounter = await builder.LoadContent(level.Encounter);               // now: Construct reads it
builder.RequestContent(level.Encounter, out ContentSlot<ArenaEncounter> slot); // before the build, with the groups; slot.Value after
await builder.FindOrLoadSceneWithServices(Scenes.Menu);                    // ContentScene
await builder.FindOrLoadScene<ArenaView>(level.Scene);
new ScopeLoadOptions(parent, Construct).WithAssetScene(Scenes.Game);
```

During the game, on the lifetime of the owner (`[Inject] IContentLoader`):

```csharp
var boss = await _content.Load(runLifetime, _options.FinalBoss);         // released when the run ends
var scene = await _content.LoadScene(lifetime, _options.Location, isMain: true);
await _content.Fetch(lifetime, _options.NextLocation);                   // files only: warms the next step
_content.Progress(_options.NextLocation).Advise(lifetime, p => _bar.Set(p.Loaded, p.Total));
var icon = _content.LoadNow(lifetime, reference);                         // only when its files are real, else throws
```

- `reference.Target` / `IsLoaded` read the reference itself; `Target` throws before the load. `EditorAsset` / `EditorPath` are for
  tools and mocks (`FindOrLoadScene` of a mock finds the opened scene by `EditorPath`).
- Catalogue groups keep their API (`Request*Group`, `Load*Group`, `Retain` / `Release`, `EnsureLoaded`); a group loads through
  `IContentLoader` by the reference in `ContentRoot`. `IsPreloaded` groups (`preloaded` in the json list of the groups; was
  `nonAddressable` / `Resources`) are fetched by `AddContent` and read synchronously.
- `Scenes.X` is a `ContentScene` taken from the root (`Scenes.X.Value` is gone) and is read only after `AddContent`. Scenes of Build
  Settings are not in it.
- An empty reference throws at the load and fails the content build.
- The inspector draws both as an object field of `T` / a `SceneAsset` (drawers of the engine, the wrappers only proxy them).
  Generators assign them with `ContentReferenceFields.AssignObject` / `AssignScene`.

## Build

`ContentBuild` runs before every player build: an `IPlayerBuildStep` around `PlayerBuildSteps.Run` (`WebGlBuild.Build`,
`deploy-client`) and a `BuildPlayerProcessor` for the build window and `-build*Player`. It:

1. calls every `IContentBuildStep` (the `ContentRoot` generator);
2. builds the directory into `Library/Content/<name>` (`FailBuildWhenErrorsLogged`);
3. checks the lazy references of the assets and scenes of the directory: empty, pointing outside it, a GUID / local id that differs
   from the id (`... (assign it in the inspector)`) - errors with the list, the build fails;
4. writes the index (`ContentIndex`: files with sizes, manifest hash, files of the roots, files of each object and scene) as
   `Assets/FrameworkContentIndex/Resources/Content/<name>.json`, read synchronously at the start; the folder is deleted after the build;
5. adds the folder to `StreamingAssets/Content/<name>` of the player (`AddAdditionalPathToStreamingAssets`, nothing in `Assets`), or,
   with `IsPreloaded` on WebGL, into the `.data` file (`Library/PlayerDataCache/WebGLPreloadedStreamingAssets/framework-content.manifest`).

The build fails without the `Content` section. File names are hashes, so a changed asset is a new URL.

## Stores

| Player | Store | Behaviour |
|---|---|---|
| Standalone, WebGL with `IsPreloaded` | `LocalContentStore` | registers `StreamingAssets/Content/<name>` as is, fetch does nothing |
| WebGL | `FetchedContentStore` over `WebContentFetch` | zeros + fetch into `temporaryCachePath/Content/<name>`; files from `<remoteUrl or StreamingAssets>/Content/<name>/`, stored in the Cache API (`caches.open('content')`, keyed by URL), so the next opening makes no request; at start every cached URL the index does not name is deleted; without Cache API (plain http) only `fetch` and one log line |
| Play Mode of the editor | `EditorContentDirectory` | nothing built or registered: `Loadable` and `LoadableSceneId` give the assets and scenes of the project (`AssetDatabase`), `Release` does not destroy them; the roots come from `Content.Roots` |

## Telemetry and problems

- Every load is `AssetLoads.Begin("Content: <guid>")` -> `Finish("Content: <name>")` (scenes `Content: scene ...`) and a span
  `GameProfiler.Concurrent("Content: <type>")`: the marks `load.*` of the client telemetry and the startup trace.
- The build log (`[ContentBuild]`): `<name>: N files, raw, brotli; root files, objects, scenes`, files no load needs, and a warning
  `built-in resources in the directory: unity_builtin_extra (...)` - a scene with `m_LightingDataAsset {20201, unity_builtin_extra}`
  copies ~0.5 MB of built-in resources into the directory (clear the field when nothing is baked; `m_SpotCookie` of `RenderSettings`
  costs nothing).
- `deploy-client` checks each WebGL output (`verify.sh`, needs `brotli`): `Library/Content/<name>` fresh, its manifest hash inside the
  `.data` (the index), without `IsPreloaded` the files of `StreamingAssets/Content/<name>` equal by names and sizes.
- `[Content] ... needs a fetch: load it with Load` - `LoadNow` of a remote object; `... is requested, read it after the build of the
  scope` - `ContentSlot.Value` too early; `... has no GUID: it was not assigned in the editor` - a reference written without the drawer.

## Limits (Unity 7000.0.0a10)

- Content directories are an alpha API; remote delivery is not supported officially: zeros + fetch rely on the behaviour, not on a
  contract. A new alpha may break it: run the content tests (`Content*`, `AssetGroupTests`) and the variant
  `content-directory-single` of `client/measurements` (branch `content-loading-size`) after every Unity upgrade.
- The manifest format (`BuildManifestHash.txt` -> `<hash>.json`, version 3) is read by `ContentManifest` only; another version throws.
- A `Loadable` / `LoadableSceneId` in a scene of the player is erased by the build: start lazy loads from the root.
- Built-in resources (`unity_builtin_extra`) referenced by the directory are copied into it (see the build log).
