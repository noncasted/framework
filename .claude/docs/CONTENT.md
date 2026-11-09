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
- **One archive, zeros + Range.** The directory ships as one archive of the engine (UnityFS, LZ4 blocks) in the order of first use: the
  manifest, the closure of the roots, the closure of every scene, of every object. A fetched store writes the archive as zeros of its full
  length, fetches its header (the table of contents) with the manifest and the roots, and registers it; the engine reads the header on the
  mount and the blocks of a file only when it opens the file, so a load first fetches the spans of its files over the zeros
  (`ContentIndex`: every file with its offset and size in the archive). Files at most `ContentOptions.RangeGap` (16 KiB) apart go into one
  Range request with the bytes between them (`ContentRanges`): a level of thousands of files takes tens of requests.
- **The times of the archive are pinned.** The engine refuses an archive changed after the mount (`Archive file was modified while it was
  opened`); MEMFS sets `mtime` on every write, so `WebContentFetch_Pin` records the times of its node before the registration and every
  write puts them back. That leans on the inside of the emscripten of Unity (`node.mtime / ctime / atime`): a node without them throws.
- **A queue of requests.** `WebContentFetch.jslib` / `WebContentQueue.jslib` let at most `ContentOptions.MaxFetches` (64) requests go to
  the server at once (too many exhaust the browser, `net::ERR_INSUFFICIENT_RESOURCES`); the others wait in the order they were asked for,
  a slot is held until the body is read, a read from the Cache API takes none, an abort in the queue starts nothing.
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
2. builds the directory into `Library/Content/<name>` (`FailBuildWhenErrorsLogged | DisableWriteTypeTree | SerializeUnityVersion`: no type
   trees, about 2/3 of the bytes; safe because the directory is built with every player, so both serialize the same types. The editor
   registers no directory without type trees: the test builds keep them);
3. checks the lazy references of the assets and scenes of the directory: empty, pointing outside it, a GUID / local id that differs
   from the id (`... (assign it in the inspector)`) - errors with the list, the build fails;
4. packs it into `Library/ContentArchive/<name>` (`ContentArchiveWriter`: `ArchiveAndCompress` LZ4 in the order of `ContentArchiveOrder`,
   `content0.archive` + `BuildManifestHash.txt`; `ContentArchiveReader` reads the table of contents back for the spans) and writes the index
   (`ContentIndex`: archive length, header, files with their spans, manifest hash, files of the roots, of each object and scene) as
   `<hash>.index.json`; the player gets only the stamp (`ContentStamp`: hash, preloaded, brotli index) as
   `Assets/FrameworkContentStamp/Resources/Content/<name>.json`, read synchronously at the start; the folder is deleted after the build;
5. gives the player build the build report directory of the content build (`BuildHistory`, its `ScriptsOnlyCache.yaml`:
   scripts, serialized classes, native classes of the directory) through `AddPreviousBuildReportDirectory`, so the linker keeps the
   types only the directory uses; without it a level loses its `CircleCollider2D` (`contained objects with undefined types ...
   stripped`). A content build without that file fails;
6. adds the shipped folder (`ContentShipment`) to `StreamingAssets/Content/<name>` of the player (`AddAdditionalPathToStreamingAssets`,
   nothing in `Assets`): read in place (standalone, `IsPreloaded` on WebGL) - the archive folder with `<hash>.index.json` in it, the
   preloaded archive into the `.data` file (`Library/PlayerDataCache/WebGLPreloadedStreamingAssets/framework-content.manifest`; the index
   stays a file fetched at the start, not verified on a game: none preloads); fetched by WebGL - `Library/ContentShipped/<name>` with
   `<hash>.archive` (the archive as it is) and `<hash>.index.json`, `.br` with brotli compression (`ContentBrotli`). Brotli with the
   decompression fallback fails the build (the loader would decode its own files, the fetch of the index would get raw `.br` bytes).

The build fails without the `Content` section. File names are hashes, so a changed asset is a new URL. Two builds of one tree give the
same manifest hash (checked on `ContentSmoke`: 4 builds in 2 editor processes, one hash).

**The server** sends `<hash>.index.json.br` with `Content-Encoding: br`, as `Build/*.br` of Unity (the generic `\.br$` rule of nginx,
`lan/serve.py`, `stand/serve_tls.py`); an index that comes in still compressed is no json and fails with `... is no json: the server must send
a .br file with Content-Encoding: br`. It answers Range requests of `<hash>.archive` with `206` and the bytes as they are: `Accept-Ranges:
bytes`, **no `Content-Encoding`** (no `gzip on` for it). Anything else (`200` with the whole file, another length, an encoding) fails the
load with its reason in the console. Both names are of one build: `Cache-Control: immutable` is safe; a missing file should be a 404.

## Stores

| Player | Store | Behaviour |
|---|---|---|
| Standalone, WebGL with `IsPreloaded` | `LocalContentStore` | registers `StreamingAssets/Content/<name>` as is, fetch does nothing |
| WebGL | `FetchedContentStore` over `WebContentArchive` | the index from `<remoteUrl or StreamingAssets>/Content/<name>/<hash>.index.json[.br]` (`WebContentIndex`, its hash checked against the stamp); `content0.archive` of zeros + `BuildManifestHash.txt` in `temporaryCachePath/Content/<name>`, ranges of `<hash>.archive` written at their offsets; the Cache API (`caches.open('content')`) keeps the index by URL and every file of a range by `<url>?offset=<n>` (the Cache API ignores a fragment), a range comes from it when it holds all its files; at start every cached entry of another URL is deleted; without Cache API (plain http) only `fetch` and one log line |
| Play Mode of the editor | `EditorContentDirectory` | nothing built or registered: `Loadable` and `LoadableSceneId` give the assets and scenes of the project (`AssetDatabase`), `Release` does not destroy them; the roots come from `Content.Roots` |

## Telemetry and problems

- Every load is `AssetLoads.Begin("Content: <guid>")` -> `Finish("Content: <name>")` (scenes `Content: scene ...`) and a span
  `GameProfiler.Concurrent("Content: <type>")`: the marks `load.*` of the client telemetry and the startup trace.
- The build log (`[ContentBuild]`): `<name>: N files, raw, archive (header), shipped in <folder>; root files, objects, scenes` (shipped:
  the archive and the index), files no load needs, and a warning
  `built-in resources in the directory: unity_builtin_extra (...)` - a scene with `m_LightingDataAsset {20201, unity_builtin_extra}`
  copies ~0.5 MB of built-in resources into the directory (clear the field when nothing is baked; `m_SpotCookie` of `RenderSettings`
  costs nothing).
- `deploy-client` checks each WebGL output (`verify.sh`, needs `brotli`, `jq`): `Library/Content/<name>` fresh, its manifest hash inside
  the `.data` (the stamp), without `IsPreloaded` `StreamingAssets/Content/<name>` exactly `<hash>.archive` (the one of
  `Library/ContentArchive/<name>`, `UnityFS`) and `<hash>.index.json.br` (its hash, the archive length, every built file).
- `[Content] ... needs a fetch: load it with Load` - `LoadNow` of a remote object; `... is requested, read it after the build of the
  scope` - `ContentSlot.Value` too early; `... has no GUID: it was not assigned in the editor` - a reference written without the drawer.

## Limits (Unity 7000.0.0a10)

- Content directories are an alpha API; remote delivery is not supported officially: zeros + Range rely on the behaviour (a lazy archive,
  the times of MEMFS), not on a contract. A new alpha may break it: run the content tests (`Content*`, `AssetGroupTests`) and the variant
  `content-directory-single` of `client/measurements` (branch `content-loading-size`) after every Unity upgrade.
- The manifest format (`BuildManifestHash.txt` -> `<hash>.json`, version 3) is read by `ContentManifest` only; another version throws.
- A `Loadable` / `LoadableSceneId` in a scene of the player is erased by the build: start lazy loads from the root.
- Built-in resources (`unity_builtin_extra`) referenced by the directory are copied into it (see the build log).
