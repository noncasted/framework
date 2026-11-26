# Content bundles

**TL;DR:** the assets a game loads after the start (catalogue groups, scenes of scopes, lazy references) live in AssetBundles of
the engine (`com.unity.modules.assetbundle`, no Addressables, no UnityWebRequest), built with every player build. A bundle is a
`ContentBundle` asset; a load holds a whole bundle with its dependencies on a lifetime. Code: `client/core/Runtime/Content`, editor
`client/core/Editor/Content` (namespace `Internal`, folders in Code layout below). The rules of the API are those of the updater
waits: a lifetime first, the lifetime owns the result. Until 0.8 (the unreleased notes) the content was one content directory of
Unity 7000 (`Loadable<T>`, Range requests of one archive): it is gone.

## Model

- **A bundle is a `ContentBundle` asset** (Create > Framework > Content Bundle): its roots (assets and scenes), `IsPreloaded`
  (a WebGL build has it in the `.data` file) and `IsGzip` (a WebGL player fetches it as `<file>.gz`, unpacked by the page). The
  name of the asset is the name of the bundle (`ContentOptions.Name`, `IContentLoadersCollection`); names are unique case aside, of letters,
  digits, `_` and `-`. Every player build builds every `ContentBundle` of the project; `Framework.json` no longer lists content
  (a file with `Content` / `ExtraContent` throws).
- **What a bundle holds.** Its roots and everything they reach, by direct references and by lazy ones (`ContentReference<T>`,
  `ContentScene`), except what another bundle holds: a walk stops at a root of another bundle, which is then a dependency (the engine
  resolves the reference when that bundle is loaded; the catalog lists the dependencies, a load brings them). A lazy target no
  bundle holds goes into the bundle that reaches it (`ContentBundleLayout`).
- **The checks of the build** (`ContentBundleLayout`, all errors at once, the build fails): a root of two bundles; an asset two bundles
  reach directly and none holds (the engine would copy it into both: make it a root of one of them or of a shared bundle); a lazy
  target no root holds that two bundles reach; an empty or broken lazy reference; a lazy reference to a child of a prefab (the
  loader finds a component of the root only) or to one of two sub-assets of one name and type. The graph is a validator only:
  nothing is loaded by it, a bundle is loaded whole.
- **Assets and scenes apart.** The engine keeps the scenes of a bundle in a bundle of their own: a `ContentBundle` with scenes ships
  `<name>.<hash>.bundle` and `<name>.scenes.<hash>.bundle`, loaded together; an asset both need goes to the assets. It is still
  held by no root: another bundle that reaches it too (also through a lazy target it took) makes it a duplicate.
- **The catalog** (`ContentCatalog`, `Resources/Content/catalog.json` of the player, read synchronously at the start): every bundle
  with its files (named by their hash: a changed bundle is a new URL), sizes, dependencies and roots, every lazy target (GUID and local
  id of its object -> bundle, the name of a sub-asset) and every scene (GUID -> bundle, path). An asset is in its bundle under its
  GUID; a scene under its path. Only lazy targets and roots are listed, not every asset.
- **Lazy references.** `ContentReference<T>` and `ContentScene` keep the GUID (and the local id) of the target, written by the
  inspector (an object field of `T` / a `SceneAsset`) or by `ContentReferenceFields.AssignObject` / `AssignScene` of a generator.
  The owner of a reference loads without its target. They work anywhere (a scene of the player too): the catalog knows the bundle.
  A reference to a component is a reference to its prefab, the loader gives the component.
- **The lifetime owns the result.** A load holds the bundle of the target and its dependencies while its lifetime lives; the last
  hold of a bundle unloads it with every object of it (`AssetBundle.Unload(true)`), and a fetched copy of its files leaves the memory of
  the page (the Cache API keeps the bytes). An AssetBundle frees its objects only with itself: a reference released while the bundle is
  held by another load (or by a registration, below) gives `IsLoaded` false, its object stays. A lifetime that ends during the load
  gives `OperationCanceledException`; a fetch nobody waits for is aborted (`AbortController`); a load of the engine already started
  ends and is unloaded at once, and the next load of that bundle waits for it (the engine refuses two loads of one file).
- **Counted per reference instance.** Two loads of the same `ContentReference` share one load; two references to one target give one
  object of the bundle. One fetch per file for every load and prefetch waiting for it.
- **Registered bundles** (the main one of `AddContent`, the ones of `AddContentBundle`) are loaded whole and held as long as the
  root: their roots are `IContentLoader.RootAssets`, their objects never unloaded. Keep such a bundle thin (lazy references only) and
  put the heavy targets into bundles of their own: they are loaded on the lifetime of their user and unloaded after it (the game of the
  jam: `Battle` holds `BattleViewsRoot`, `BattleViews` its four catalogues; `Stream` holds `StreamRoot`, `Music` the theme).

## API

Root scope, once, before the first load (`ContentOptions(name, remoteUrl, maxFetches)`: `name` the main bundle, the one whose roots
hold the generated `ContentRoot`, a constant of the code: the options of the game are in the bundles and load after `AddContent`;
`remoteUrl` for a CDN, empty = `StreamingAssets` of the page; `maxFetches`):

```csharp
await builder.AddContent(new ContentOptions(ContentName));   // IContentLoader, IContentLoadersCollection, CatalogContent bound, preloaded groups fetched
```

Scopes:

```csharp
var encounter = await builder.LoadContent(level.Encounter);               // now: Construct reads it
builder.RequestContent(level.Encounter, out ContentSlot<ArenaEncounter> slot); // before the build, with the groups; slot.Value after
await builder.FindOrLoadSceneWithServices(Scenes.Menu);                    // ContentScene
await builder.FindOrLoadScene<ArenaView>(level.Scene);
new ScopeLoadOptions(parent, Construct).WithAssetScene(Scenes.Game);
```

More bundles with roots of their own, root scope, after `AddContent`: each with its own loader, never bound to the catalogues. The
registration fetches and loads the bundle whole in the background (the root and the scopes after it do not wait); a failure is
logged and thrown to `Wait`:

```csharp
builder.AddContentBundle("Stream");                                  // at once, in the background
builder.AddContentBundle("Battle", ContentRegistration.OnFirstWait); // nothing fetched before the first Wait of "Battle"

var stream = await _bundles.Wait(lifetime, "Stream");              // [Inject] IContentLoadersCollection; once registered
// _bundles.Get("Stream") is the same loader after that and throws before; Get of the main name = IContentLoader
var root = stream.RootAssets<StreamRoot>().Single();               // a root of the bundle, loaded with the registration
var music = await stream.Load(lifetime, root.MenuMusic);           // a target in the bundle Music: fetched and loaded now
stream.Progress(root.MenuMusic).Advise(lifetime, p => _bar.Set(p.Loaded, p.Total));
```

Any loader loads any reference: the catalog knows its bundle (the loader of a bundle differs only by its roots).

During the game, on the lifetime of the owner (`[Inject] IContentLoader`):

```csharp
var boss = await _content.Load(runLifetime, _options.FinalBoss);         // its bundle unloaded when the run ends
var scene = await _content.LoadScene(lifetime, _options.Location, isMain: true); // the bundle let go after the unload of the scene
await _content.Fetch(lifetime, _options.NextLocation);                   // the files only (a prefetch): warms the next step
_content.Progress(_options.NextLocation).Advise(lifetime, p => _bar.Set(p.Loaded, p.Total));
var icon = _content.LoadNow(lifetime, reference);                         // the bundle loaded or its files on the device, else throws
```

- `reference.Target` / `IsLoaded` read the reference itself; `Target` throws before the load. `EditorAsset` / `EditorPath` are for
  tools and mocks (`FindOrLoadScene` of a mock finds the opened scene by `EditorPath`).
- Progress counts the bytes of the files of the bundle and its dependencies a player fetches, a file at a time: `Total` is 0 for
  bundles on the device (standalone, Android, preloaded, Play Mode).
- Catalogue groups keep their API (`Request*Group`, `Load*Group`, `Retain` / `Release`, `EnsureLoaded`); a group loads through
  `IContentLoader` by the reference in `ContentRoot`. `IsPreloaded` groups (`preloaded` in the json list of the groups) are fetched by
  `AddContent` and read synchronously. The groups are lazy targets of the main bundle unless a root of another one holds them.
- `Scenes.X` is a `ContentScene` taken from the root and is read only after `AddContent`. Scenes of Build Settings are not in it.
- An empty reference throws at the load and fails the build.
- `ContentEntryPoint.RegisterMain(lifetime, options)`: the main bundle without a scope (a smoke player, a tool).

## Build

`ContentBuild` runs before every player build: an `IPlayerBuildStep` around `PlayerBuildSteps.Run` (`WebGlBuild.Build`,
`deploy-client`) and a `BuildPlayerProcessor` for the build window and `-build*Player` (`ContentBuildPlayerProcessor`). It:

1. calls every `IContentBuildStep` with the `ContentBuildContext` of the build (`Target`, `Exclude(name)`): the `ContentRoot` generator
   (`ContentRootBuildStep` of the catalogues), the generators of the game, a step that leaves bundles out (below);
2. writes the shared bundles of the game (`ContentSharedSync`, below);
3. fixes the bundles left out (`ContentBuildContext.ExcludedBundles`; a name no bundle has fails the build) and calls `BeforeLayout`
   of every `IContentLayoutStep` (below);
4. lays out every `ContentBundle` of the project and checks it (`ContentBundleLayout`, above);
5. builds them with the Scriptable Build Pipeline (`ContentBundleBuilder` over `ContentPipeline.BuildAssetBundles`, package
   `com.unity.scriptablebuildpipeline`, a dependency of the core) into `Temp/ContentBundles/<target>` (LZ4 by chunks, no type trees:
   about a third of the bytes, safe because the bundles are built with every player; the editor loads no bundle without them, so the
   tests keep them). Its cache (`Library/BuildCache`) builds again only what changed. Not `BuildPipeline.BuildAssetBundles`: it writes
   the type trees into every scene bundle whatever the options, and the player then checks each type against them and compiles that
   code of the wasm on the first scene (+100 ms on the menu scene of the autobattler on a phone, `docs/STARTUP_OPTIMIZATION.md` there).
   Then `AfterBundles` of the layout steps, also when the layout or the build failed;
6. ships the files under the names of the build into `Library/ContentShipped` (`ContentShipment`): as they are, a fetched `IsGzip`
   bundle of WebGL as `<file>.gz` alone (gzip at the smallest size);
7. writes the catalog (`ContentCatalogWriter`) into `Assets/FrameworkContentStamp/Resources/Content/catalog.json` (deleted after the
   build) and, for the preloaded bundles of WebGL, the preload manifest that puts their files into the `.data`
   (`Library/PlayerDataCache/WebGLPreloadedStreamingAssets/framework-content.manifest`; both `ContentPlayerStamp`);
8. writes `Library/ContentBundles.link.xml`, the types of every object of the bundles (`ContentLinkXml`, `preserve="nothing"`), and gives
   it to the linker (`ContentLinkerProcessor`, an `IUnityLinkerProcessor`): a type only a bundle uses (a collider of a level, a
   component of a prefab) is not stripped, the engine keeps its native class. The serialized classes of their data
   (`ContentSerializedType`, from `WriteResult.includedSerializeReferenceFQN` of the build) go in with `serialized="true"`, as in
   Unity's `SerializedTypes.xml`, which covers only the data of the player: a class only a `[SerializeReference]` of a bundle names
   (an enemy behaviour, a projectile movement) is not stripped either; an FQN that is not `<assembly>:<type>` fails the build. The bundle build writes no build report for
   `AddPreviousBuildReportDirectory`, and `BuildPlayerContext.BuildPlayerOptions` (`assetBundleManifestPath`) is read only;
9. gives the shipped folder to the player as `StreamingAssets/Content` (`AddAdditionalPathToStreamingAssets`, nothing in `Assets`);
   a preloaded bundle of WebGL is in the `.data` file instead.

**Bundles left out of a build.** A step decides per build, the project keeps the asset (a stress bundle only in the stress profile):

```csharp
public sealed class StressContentStep : IContentBuildStep
{
    public void BeforeContentBuild(ContentBuildContext build)
    {
        if (BuildProfile.GetActiveBuildProfile()?.scriptingDefines.Contains("PLATFORM_STRESS") != true)
            build.Exclude("Stress");
    }
}
```

A bundle left out is not built, shipped or in the catalog, and the `ContentRoot` of the build has no group or scene that is its root
(written without them for the bundle build, in full again after it: `ContentRootBuildStep`, a layout step, below). It still takes part
in who holds what, so the other bundles hold the same as in a full build; a built bundle that references, directly or lazily, what it
holds fails the build (`... held by the content bundle Stress, which this build leaves out`). A name no bundle has fails too.

**Steps around the layout.** What needs the final set of the bundles left out, whatever the order of the steps that left them out,
is an `IContentLayoutStep` (found, created and ordered as the `IContentBuildStep`s: parameterless, by the full type name; a class may be
both). `BeforeLayout(build)` runs after every `BeforeContentBuild` and the shared bundles, right before the layout; `AfterBundles(build)`
after the bundle build, also a failed one, in the reverse order, only for the steps whose `BeforeLayout` returned. From the phase on
`build.ExcludedBundles` gives the `ContentBundle` assets left out (it throws before) and `build.Exclude` throws. The phase runs in every
build: `ContentRootBuildStep` of the catalogues writes the `ContentRoot` without the roots of the bundles left out in `BeforeLayout` and
in full in `AfterBundles`, nothing when none is left out; the content build itself knows no generator of the catalogues.

**Shared bundles.** An asset two bundles need directly and none holds fails the layout; the layout also gives these as data
(`ContentBundleLayout.Inspect(bundles, excluded)`: `Duplicates` with the path and the bundles, `Errors`, nothing thrown). The game declares
its shared bundles once, one class of its editor code:

```csharp
public sealed class GameSharedBundles : IContentSharedBundles
{
    public string Folder => "Assets/Common/Internal/Editor/Content";     // the game's folder: <Folder>/<Name>.asset
    public IReadOnlyList<ContentSharedBundle> Bundles { get; } = new[]
    {
        new ContentSharedBundle("SharedCommon", isPreloaded: true, isGzip: false, "Main"),  // what the main bundle shares
        new ContentSharedBundle("SharedLobby", isPreloaded: false, isGzip: true, "Lobby"),
        new ContentSharedBundle("SharedGamePlay", isPreloaded: false, isGzip: true)         // serves none listed: the rest
    };
}
```

`ContentSharedSync` lays out the other bundles, makes every duplicate a root of the first shared bundle that serves one of the bundles
needing it (or is one of them), and lays out again until none is left; any other error of the layout fails it. The shared bundles are
its output: written from nothing on every sync (a root no longer shared leaves, an empty bundle is deleted, a sync with nothing new
writes no file), a bundle of a shared name outside the folder is an error. It runs before every content build (after the build
steps, before the layout steps), in Regenerate All (after the content root) and from Tools/Framework/Codegen/Sync Shared Content
Bundles. An explicit list rather than a flag on `ContentBundle`: which shared bundle takes an asset is an ordered rule of the game (the
phase that loads it first), a flag cannot say it.

The build opens no scene: an open scene with unsaved changes stops it (`The open scenes ... have unsaved changes`; the Scriptable Build
Pipeline builds nothing then). Save them; a tool or a test opens a new scene first and restores its scenes after.

The log (`[ContentBuildLog]`, the totals under `[ContentBuild]` and `[ContentBundleBuilder]`): per bundle its files and bytes,
how the player gets it, dependencies, roots, assets, scenes, lazy targets, the assets the engine copied in, and a warning for
built-in resources copied in.

**The server** sends a `.bundle` and a `.gz` as they are (a `.gz` with or without `Content-Encoding: gzip`: a body without the gzip
magic is taken as decoded); a plain `.bundle` it may gzip itself. All names are of one build: `Cache-Control: immutable` is safe.

## Distribution

The root (`ContentEntryPoint`) picks the source of the loaders and, in a player, the distribution of the platform (`#if`); the files of
a bundle come through `ContentFileFetches` (one fetch per file, `ContentFileFetch`, for every load and prefetch waiting for it; the
bytes of the progress), `ContentBundleStorage` holds the bundles of the engine over them. The folder and the names of the shipped files
(`Content`, `<file>.gz`) are `ContentShippedPaths`, the same for the editor build.

| Player | Distribution | Behaviour |
|---|---|---|
| Standalone, Android, a test folder | `LocalContentDistribution` | `StreamingAssets/Content/<file>` read in place by `AssetBundle.LoadFromFile` (on Android inside the APK); every file real, progress `Total` 0 |
| WebGL | `WebContentDistribution` | `Open(catalog, options)`; a preloaded bundle in `StreamingAssets/Content` of the emscripten file system (the `.data`); the others fetched from `<remoteUrl or StreamingAssets>/Content/<file>[.gz]` into `temporaryCachePath/Content/<file>` (`WebContentFetch`: the Cache API `content` keeps them by URL, unpacked; at the start every cached entry with `/Content/` of a file of another build is deleted) and deleted when their bundle is unloaded; at most `MaxFetches` requests at once (`WebContentQueue.jslib`) |
| Play Mode of the editor | none: `EditorContentSource` | nothing built or loaded: a reference gives the asset of the project (`AssetDatabase` by GUID and local id), a scene is loaded from its asset (`EditorSceneManager.LoadSceneAsyncInPlayMode`), a release destroys nothing; the roots are those of the `ContentBundle` asset |

## Code layout

Folders from the top down: the root, the levels it wires one under another, the data and the types every level shares at the
bottom; a row is what the folder holds and who calls it.

`client/core/Runtime/Content`:

| Folder | What | Called by |
|---|---|---|
| `Content/` | `ContentEntryPoint`: the content of a root scope - the source of each bundle (the bundles of the build in a player, the assets of the project in Play Mode), the distribution of the platform, the loaders and the loads they share, `RegisterMain` without a scope; `ContentOptions`, its input | `Scopes/`, a smoke player |
| `Scopes/` | `ContentScopeExtensions` (`AddContent`, `AddContentBundle`, `LoadContent`, `RequestContent`), `ScopeContent` (the loader of a scope, its parent's), `ContentSlot`, `ContentRegistration`; they delegate to the root | the scopes of the game, the scope and scene loaders of the core |
| `Loading/` | `IContentLoadersCollection` / `ContentLoadersCollection` (the loaders of the root by the name of their bundle: `Get`, `Wait`), `IContentLoader` / `ContentLoader`, `ContentLoadsCollection` (the loads and the progress the loaders of a root share), `ContentObjectLoad`, `ContentSceneLoader`, `ContentLoadedScene`, `ContentLoadPhase`, `ContentProgressReport` | the root, `Scopes/`, the game, `CatalogContent` of the catalogues |
| `Sources/` | `IContentSource`, what a loader loads from: `BundleContentSource` (a player), `EditorContentSource` (Play Mode) made by `EditorContentSourceFactory` (installed by `Internal.Editor`), `ContentTarget` (the component of a prefab) | `ContentLoader`, the root |
| `Storage/` | `ContentBundleStorage` (the bundles of the engine held by lifetimes, a hold with its dependencies), `ContentStoredBundle` (one bundle and its holders) | `BundleContentSource` |
| `Distribution/` | `IContentDistribution` (where the files are on the platform), `LocalContentDistribution`, `ContentFileFetches` / `ContentFileFetch` (one fetch per file), `ContentShippedPaths` | the storage, `BundleContentSource`, the root; `ContentShippedPaths` also the editor build |
| `Distribution/Web/` | `WebContentDistribution`, `WebContentFetch` with `WebContentFetch.jslib`, `WebContentQueue.jslib`: a WebGL player only (`#if UNITY_WEBGL && !UNITY_EDITOR`) | the root |
| `Catalog/` | `ContentCatalog` and its entries `ContentCatalogBundleEntry`, `ContentCatalogFileEntry`, `ContentCatalogObjectEntry`, `ContentCatalogSceneEntry`: the data of the build | every level; written by `ContentCatalogWriter` of the editor |
| `Common/` | `ContentReference` / `ContentReference<T>`, `ContentScene`, `ContentProgress`, `ContentEditorAssets` (the object of a GUID and a local id in the editor) | every level, the games, the editor |

`client/core/Editor/Content`:

| Folder | What | Called by |
|---|---|---|
| `Content/` | `ContentBuild`: the content build of every player build (`IPlayerBuildStep`; `Prepare` / `Cleanup`, `Current`: the bundles of the running build) | `PlayerBuildSteps`, `Player/`, the tests |
| `Steps/` | `IContentBuildStep`, `IContentLayoutStep`, `ContentBuildContext`: the API of the steps | implemented by the game and the catalogues (`ContentRootBuildStep`), called by `ContentBuild` |
| `Shared/` | `IContentSharedBundles`, `ContentSharedBundle`, `ContentSharedSync`: the shared bundles of the game | `ContentBuild`, Regenerate All, the menu Codegen |
| `Layout/` | `ContentBundleLayout` (`Lay`, `Inspect`: the layout and its checks), `ContentOwnership` (who holds what), `ContentAssetOwner`, `ContentBundlePlan` (what goes into one bundle), `ContentDuplicate` | `ContentBuild`, `ContentSharedSync` |
| `Layout/Graph/` | `ContentAssetGraph` (the direct references of the project), `ContentLazyReferenceScanner` (the lazy references of an asset or a scene), `ContentLazyReference`, `ContentLazyTargets` | `Layout/`, `ContentSharedSync` |
| `Pipeline/` | `ContentBundleBuilder` (the Scriptable Build Pipeline), `ContentBuiltContent` (the bundles and the serialized classes of their data), `ContentBuiltBundle`, `ContentBuiltFile`, `ContentBuildTargetSelection`, `ContentUnsavedScenes` | `ContentBuild` |
| `Output/` | `ContentShipment` (`Library/ContentShipped`), `ContentCatalogWriter`, `ContentLinkXml`, `ContentSerializedType`, `ContentBuildLog`, `ContentBuildOutput` (what `Prepare` gives the player build) | `ContentBuild` |
| `Player/` | `ContentBuildPlayerProcessor` (`StreamingAssets/Content`; prepares a build outside `PlayerBuildSteps`), `ContentLinkerProcessor`, `ContentPlayerStamp` (the catalog in `Assets/FrameworkContentStamp`, the preload manifest of WebGL) | the callbacks of the player build, `ContentBuild` |
| `Definitions/` | `ContentBundle` (the asset of a bundle, declared by the game), `ContentBundleAssets` (every `ContentBundle` of the project) | `ContentBuild`, `Shared/`, `Layout/`, `PlayMode/` |
| `Drawers/` | `ContentReferenceDrawer`, `ContentSceneDrawer`, `ContentReferenceFields` (writes and reads a reference: the drawers, the tools, the generators) | the inspector, the generators of the game and the catalogues, `Layout/Graph/` |
| `PlayMode/` | `EditorContentSourceInstall`: the roots of the `ContentBundle` assets for `EditorContentSourceFactory` | the load of the editor |

## Telemetry and problems

- The startup trace has `Content: catalog`, `Content: bundle <name>` (a synchronous load of a bundle, and a concurrent span of an
  asynchronous one), `Content: roots <name>`, `Content: bind catalogues`, `Group: <name>` and `Content: load <type>` (a synchronous
  `LoadNow`).
- Every load is `AssetLoads.Begin("Content: <guid>")` -> `Finish("Content: <name>")` (scenes `Content: scene ...`) and a span
  `GameProfiler.Concurrent("Content: <type>")`.
- `deploy-client` checks each WebGL output (`verify.sh`): every file of `Library/ContentShipped` in `StreamingAssets/Content` as shipped or
  preloaded, named by the catalog in the `.data`, a bundle each, nothing of another build.
- `[Content] ... needs a fetch: load it with Load` - `LoadNow` of a bundle not on the device; `... is loading asynchronously` - `LoadNow`
  during a `Load` of the bundle; `... is requested, read it after the build of the scope` - `ContentSlot.Value` too early;
  `... is in no content bundle` - a reference the build never saw (made in code).

## Limits (Unity 7000.0.0a10)

- A held bundle frees no object before it is unloaded: memory follows the bundles, not the references.
- The objects of the scenes of the player (Build Settings) and of the bundles are not checked for duplicates: an asset both use is in
  the `.data` and in a bundle.
- Built-in resources (`unity_builtin_extra`) referenced by a bundle are copied into it (see the log).
