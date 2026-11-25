# Client core

- **Client core.** Assemblies `Internal`, `Internal.Editor`, `Internal.Tests` (namespace `Internal`, asmdef GUIDs of
  the projects kept; no uGUI in them: the helpers of uGUI (`ListenClick`, `ButtonSounds`, `Image.SetAlpha`, `Slider.ToLifetimedValue`)
  are `Internal.UGUI` (`client/core/UGUI`, `GUID:1a5b96d3d6de4210867a24fe0a1f3fcb`, compiled only with `com.unity.ugui`);
  the decoder of mp3 clips of the audio groups is `Internal.EncodedAudio` (`client/core/EncodedAudio`, registers itself, the only
  user of `UnityWebRequest` in the core; a game without mp3 lists it in `StrippedPlayerAssemblies`); `Internal` is `GUID:f039cb8c565843dba6da5b3c1ccde7d6`, `Framework.Common`
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
  assembly, module roots, scene folders, art and audio folders, favorite scenes, player assemblies to strip (the bundles of the content
  are `ContentBundle` assets, not settings: a file with `Content` / `ExtraContent` throws). A game without the settings gets
  the error. The live graphs write into `<ContentFolder>/Editor/Transient`: the project ignores `Transient*`
  there in git. Editor assets of the framework are loaded from `FrameworkSettings.Package(...)`.
- **Screen canvases.** The canvases the framework builds from code (the latency overlay of `network`, the outdated
  client notice of `ui`) go where the game says in its root scope (`IScreenCanvasCamera` of the core): `AddScreenCanvasCamera(camera,
  layer, planeDistance)` (Screen Space - Camera: the whole canvas on the layer, the camera asked on every show) or, explicitly,
  `AddScreenCanvasOverlay()`. Without one the scope that builds them does not resolve. The Overlay mode lost its projection on
  iPadOS 18.2 Safari; the camera and its drawing stay in the game.
- **Content.** AssetBundles of the engine, no Addressables (removed in 0.8). The whole contract is `CONTENT.md`: bundles and
  their layout, the build and its steps, shared bundles, distributions, references, loading in scopes and during the game.
  In short: a bundle is a `ContentBundle` asset, built with every player build; the root scope calls
  `await builder.AddContent(new ContentOptions(name))` once (the name of the main bundle is a constant of the root scope:
  the options of the game load after it); during the game `IContentLoader` loads on the lifetime of the owner, which holds
  the whole bundle of the target with its dependencies and unloads it after the last hold; Play Mode loads the assets of
  the project.
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
- **Logging.** Runtime code logs through `ILog<T>` (a parameter of a constructor or an `[Inject]` method, given by every
  scope without a registration; a static class takes `Logs.Of(typeof(X))`), not `Debug.Log*`. Templates, levels (`LogOptions`
  of the `OptionsContainer` of the game), sinks and what the telemetry takes: `LOGGING.md`.
- **Updater.** The global scope calls `AddUpdater(prefab)` (core); delays without coroutines:
  `updater.RunDelay(lifetime, time)`; every updater wait (RunDelay, RunUpdateAction, Progression, Tween) ends with
  `OperationCanceledException` when its lifetime ends first. `Pause` / `Continue` are counted (Continue without Pause
  throws); an updatable that throws does not stop the others (reported through `ReactiveExceptions`).
- **Prefab pool.** `PrefabPool<T>` (`Runtime/Services/Pools`): instances of a template under one parent; `Get(lifetime)` takes one
  for that lifetime and gives it back when the lifetime ends (inactive while free), `created` runs once per instance (injection
  with `IViewInjector`, layers), the user resets the state on every take; the lifetime of the pool destroys every instance.
- **Hierarchy bindings.** Generated `*Bindings.g.cs` (`ObjectBindings`) go to `Assets/Generated` of a module root
  (`FrameworkSettings.ModuleRoots`), to `Generated` next to any other asmdef, to
  `Runtime/Tools/PrefabHierarchy/Generated` of the content folder for an owner from the content assembly; an asset
  outside every asmdef is an error. One object may carry several bindings classes: a prefab variant keeps the ones
  of its base and mirrors only what it added (`_layerAssetGuid`). Prefab batch:
  `HierarchyBindingsBatch.RegeneratePrefabs`, then `HierarchyBindingsGenerator.PumpPending()` until `Idle.` when the
  editor is not focused; check with `HierarchyBindingsValidator`.
- **`[Inject]` methods** of a type that may be registered from another assembly are `public`: the generator does
  not see an `internal` method of a foreign assembly and does not call it (diagnostic CINGR010).
