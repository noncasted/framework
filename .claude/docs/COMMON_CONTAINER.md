# Dependency Injection: the Internal Container

**TL;DR:** the client uses its own DI container (`namespace Internal`) whose concrete container classes are generated at compile time by a Roslyn source generator. Services are plain classes with constructor injection, registered in installer extension methods (`builder.Register<T>().As<I>()`). A scope has one phase, `IScopeSetup.OnSetup(lifetime)` with the scope `IReadOnlyLifetime`; any other order is a call written by the owner of the scope. MonoBehaviours are the exception: they are registered with `RegisterComponent` and get dependencies through a method marked `[Inject]`.

There is no reflection fallback. A scope root without a generated class throws `No generated container for '<root>'`; a missing dependency is a compile error (CINGR003), not a runtime null.

Code:
- Runtime: `client/core/Runtime/Tools/Container/` (Abstract, Generated attributes, Runtime) and `client/core/Runtime/Scopes/` (builders, loaders, lifecycle interfaces, entity scopes)
- Generator: `tools/framework/client/core/Source~/ContainerGenerator/` (pipeline, emitted code and diagnostics are described in its `README.md`), built dll in `tools/framework/client/core/Analyzers/`
- Editor tools: `tools/framework/client/core/Editor/Tools/Container/` (Container Debugger), `.../ContainerGraph/` (Container Graph), `.../ContainerCodegen/` (asset map writer)
- Tests: `tools/framework/client/core/Tests/Editor/Container/` (codegen emit/diagnostic tests, benchmarks)

## Scopes

A scope = one generated container + one `ILifetime` + optionally a service scene. Scopes form a tree; a child container resolves what it does not register from its parent.

```
Internal  (InternalScopeExtensions.Construct)          client/Assets/Common/Internal/Runtime/InternalScopeExtensions.cs
  Global  (GlobalScopeExtensions.Construct)            client/Assets/Common/Global/Setup/GlobalScopeExtensions.cs
    Meta  (MetaScopeExtensions.Construct)              client/Assets/Meta/MetaScopeExtensions.cs
      Menu, MenuBackground, Lobby, GameLoop, GamePlay  (Menu/..., Common/Flow/Loop/..., GamePlay/Loop/GamePlayScopeExtensions.cs)
        entity scopes (player, weapon, enemy, room, chest, ...) built by factories in GamePlay
```

### Service scope (`IScopeBuilder`)

A root is a static non-generic `Construct` method of a non-generic type taking `IScopeBuilder` (optionally plus one argument). The type may be nested: the generated class stays in the namespace of the type, as `{Outer}_{Type}{Method}Container`. It is passed to `ScopeLoadOptions` as a **method group**, never a lambda: the generator finds the scope class by the method (`GeneratedScopes.RootId`). The parent is declared twice: at runtime by the `ILoadedScope parent` argument and at compile time by `[ContainerScopeParent]`.

```csharp
public static async UniTask<ILoadedScope> LoadGlobal(this IServiceScopeLoader loader, ILoadedScope parent)
{
    var options = new ScopeLoadOptions(parent, Construct)
        .WithRuntimeScene("Global_Services");

    var scope = await loader.Load(options);
    // A scope with an entry (the loop of the game) is started here: scope.Resolve<GameLoop>().Start(scope.Lifetime)

    return scope;
}

[ContainerScopeParent(typeof(InternalScopeExtensions), nameof(InternalScopeExtensions.Construct))]
public static async UniTask Construct(IScopeBuilder builder)
{
    await builder.LoadEnvAssetGroup(GlobalAssets.Group);   // needed while registering
    builder.AddUpdater();
    builder.AddUI();
}
```

- Root with an argument: `ScopeLoadOptions.Create(parent, Construct, sessionData)` and `Construct(IScopeBuilder builder, SessionData sessionData)`.
- Scene for the scope objects: `WithRuntimeScene(name)` (empty scene created at runtime), `WithAssetScene(contentScene)` (a `ContentScene` of a content bundle, `CONTENT.md`), or nothing (`NoScene`: `builder.Instantiate` / `Binder.MoveToModules` throw).
- `AsMock()` marks a scope as part of a mock; installers read `builder.IsMock`.
- `ILoadedScope.Dispose()` disposes the child scopes, terminates the lifetime, unloads the scope scenes, disposes the container and only then releases asset groups (`AddAfterDispose`): lifetime listeners run in an order not tied to assets (session entities die before remote users) and may still read the catalogues.

Loading order inside `ServiceScopeLoader.Load` (`tools/framework/client/core/Runtime/Scopes/Services/ServiceScopeLoader.cs`):

```
1. scene for the scope (options.ServiceScene)
2. Construct(builder)             registrations; Load*Group awaits here
3. Events.InvokeBeforeBuild()     Request*Group asset groups are retained here
4. ScopeContainer.Create(...)     generated container, services constructed
5. RunSetup                       OnSetup of every IScopeSetup
6. LoadedScope                    the caller starts what the scope runs (its loop, its session)
```

### Entity scope (`IEntityBuilder`)

Short-lived scopes for prefab instances (player, weapon, enemy, room). Built by `IEntityScopeLoader.Load(parentLifetime, parentContainer, view, BuildX)` where `view` is a `ScopeEntityView` subclass on the prefab and `BuildX` is a local function or method group with `[ContainerScopeParent]`. The view registers every `IEntityComponent` found under it (generated `*Bindings`), see `HIERARCHY_BINDINGS.md`.

```csharp
var loadResult = await _entityScopeLoader.Load(lifetime, _parentScope, view, BuildLocalGrand);
var player = loadResult.Get<ILocalPlayer>();

[ContainerScopeParent(typeof(GamePlayScopeExtensions), nameof(GamePlayScopeExtensions.Construct))]
void BuildLocalGrand(IEntityBuilder builder)
{
    builder.RegisterInstance(character);
    builder.Register<StateMachine>().As<IStateMachine>();
    builder.AddPlayerSync().AddPlayerViews().AddPlayerLocalComponents();
}
```

Example: `client/Assets/GamePlay/Players/Services/GamePlayerFactory.cs`. The entity pattern itself is in `GAMEPLAY_ARCHITECTURE.md` § Entity.

**One graph per root.** The generator builds the graph of a root statically, all branches at once. Variants (local/remote, per character) are separate roots selected by a C# `switch` at the call site, not `if` inside one root. Only `bool` and closed enum parameters may change the graph of an entity root (CINGR005); for platform choices use `#if`, not `if` (see `client/Assets/Common/Global/UI/GlobalUIExtensions.cs`). An entity root used with several `ScopeEntityView` subtypes gets one generated class per view type (`GeneratedScopes.RegisterVariant`); one view type must not be shared by two prefabs (CINGR006).

The container lifetime and the entity scope lifetime are linked both ways (`EntityScopeLoader.CreateContainer`): ending either ends the entity.

## Registration API

Defined in `tools/framework/client/core/Runtime/Scopes/Common/Builders/BuilderExtensions.cs` (`Instantiate`, `FindOrLoadSceneWithServices` in `Scopes/Services/Builder/ScopeBuilderExtensions.cs`). The generator recognizes these methods by `[ContainerPrimitive(ContainerPrimitive.X)]` (`Runtime/Tools/Container/Generated/ContainerPrimitiveAttribute.cs`), not by name or class: a primitive can be renamed or moved, a new one needs a new kind in both the enum and the generator mirror (`tools/framework/client/core/Source~/ContainerGenerator/Graph/PrimitiveKind.cs`, same numeric values). Only a call that does not bind (code that does not compile yet) falls back to the method name.

| Call | Meaning |
|------|---------|
| `builder.Register<T>()` | Type constructed by the generated container (constructor injection), registered as itself |
| `.As<I>()` / `.As(type)` / `.AsSelf()` | Additional service types for the same instance |
| `builder.RegisterInstance(value)` | Ready instance (throws on null) |
| `builder.RegisterComponent(component)` | Unity object instance; its `[Inject]` method is called by the container |
| `.WithParameter(value)` | Constructor parameter supplied by value |
| `.WithScopeLifetime()` | Passes the scope `IReadOnlyLifetime` as a constructor parameter |
| `.AsSelfResolvable()` | Construct even if nobody depends on it |
| `builder.Inject(instance)` | Call the `[Inject]` method of an already created object at build time |
| `builder.Injectable<T>()` | Allow `IContainer.Inject(T)` / `IViewInjector` for runtime-created `T` (CINGR008 if `T` has no visible `[Inject]` method) |

Installers are extension methods returning the builder so they chain: `public static IScopeBuilder AddUI(this IScopeBuilder builder)`. The `ServiceLifetime` argument of `Register` is read by the generator; the default is `Singleton` per scope.

Constructor parameters are bound at compile time to: a registration in this scope, an export of a parent scope, a collection (`IReadOnlyList<T>` = all registrations of `T`), a `WithParameter` value, or `IContainer` (the current container, used by factories as the parent of entity scopes).

### WithParameter belongs to its registration and wins

`WithParameter` gives the value to its own registration only (`request.GetParameter<T>(typeof(Implementation), n)`). A constructor parameter that can hold the value (the exact type first, then an interface or a base class of it) takes it before anything else: a registration of the type in the scope (written before or after), a parent scope, and also what the scope gives by itself (`IContainer`, `ILifetime`, `IReadOnlyLifetime`). So `.WithParameter(builder.ScopeLifetime)` gives the class `ScopeLifetime`, not the lifetime of the container. Several values that fit one parameter without an exact one are CINGR014. A `WithParameter` that no parameter can hold is not used: warning CINGR015.

## Setup

One phase: `IScopeSetup.OnSetup(lifetime)` (`tools/framework/client/core/Runtime/Scopes/Common/Events/IScopeSetup.cs`), called by
`EventLoop.RunSetup` once the container is built. Register a service under it to get the call:
`builder.Register<X>().As<IX>().As<IScopeSetup>()`. A type that implements `IScopeSetup` and no registration of which lists it is
CINGR018. The lifetime is the scope lifetime: every `Advise` / `View` / `Listen` made there ends with the scope (see `COMMON_LIFETIMES.md`).

Listeners run one by one in the order of their registrations, and that order means nothing. Three rules replace the phases:

- **Ready from the constructor.** What a service needs to be used is done in its constructor (`[Inject]` method of a MonoBehaviour):
  its own state, the values of its dependencies (`Settings<TSave>` takes its save from the loaded `ISaves`). It does no I/O: a
  storage is read by an explicit, awaited call in the installer (`await builder.AddSaves(saves)` calls `ISaves.Load()`).
  The container builds the dependencies of a service before the service, so a constructor reads them ready, and the setup of any
  service may already use it (`AudioPlayer` plays, `GlobalCamera` enables, `ISafeArea.Area` has its value). A constructor that needs
  the lifetime takes `IReadOnlyLifetime` (the lifetime of the container) or `.WithScopeLifetime()`.
- **`OnSetup` starts behaviour.** Subscriptions, the updater, the reactions to the values of other services; it never reads state
  another `OnSetup` prepares.
- **Any other order is a call.** What must happen before the services exist (the saves, a platform API: `AddSaves`,
  `SteamApi.Start` in `AddSteam`) is a call in the installer; what runs once the scope is loaded (the loop of the game, a session, a warmup that the start waits for) is
  called by the owner after `Load`: `scope.Resolve<GameLoop>().Start(scope.Lifetime)`. Work that must not hold the scope (a
  connection) is started from `OnSetup` without awaiting, and its readiness is a property others wait for.

There is no async phase: a load of the scope waits only for what its owner writes there. There is no dispose phase: teardown is
`lifetime.Listen(...)` on the lifetime of `OnSetup`, which ends before the container is disposed and the asset groups are released
(an entity sets its services up on a child of its lifetime that its teardown ends first).

`IEventLoop.RunCustom<T>(lifetime, invoker)` is an event of the owner: every registration of `T` is called at the moment the owner
chooses (`IGameStarted`, `IMetaSetupCompleted`), one by one, each under its own span of the profiler.

## Plain Service (default)

```csharp
// tools/framework/client/modules/ui/Runtime/Navigation/UINavigation.cs (reduced)
public class UINavigation : IUINavigation, IScopeSetup, IUpdatable
{
    public UINavigation(IUpdater updater, IInputScheme scheme, IGamepadReader gamepad)
    {
        _updater = updater;
        _scheme = scheme;
        _gamepad = gamepad;
    }

    private readonly IUpdater _updater;
    private readonly IInputScheme _scheme;
    private readonly IGamepadReader _gamepad;

    public void OnSetup(IReadOnlyLifetime lifetime)
    {
        _updater.Add(lifetime, this);
    }
}

// installer: tools/framework/client/modules/ui/Runtime/UIExtensions.cs
builder.Register<UINavigation>()
       .As<IUINavigation>()
       .As<IScopeSetup>();
```

## MonoBehaviour Services

Use a MonoBehaviour only when the object lives in a scene or prefab. Two ways to get it into a scope:

**Scene service.** Implement `ISceneService.Create(IScopeBuilder)`. The scene holds a `SceneServicesFactory` whose serialized list of services is filled by the editor menu `Assets/Scan services` (Ctrl+E, `tools/framework/client/core/Editor/SceneServicesScanner.cs`); the installer loads the scene with `await builder.FindOrLoadSceneWithServices(sceneReference)`, which calls `Create` on every listed service.

```csharp
[DisallowMultipleComponent]
public class WallZones : MonoBehaviour, ISceneService, IWallZones
{
    public void Create(IScopeBuilder builder)
    {
        builder.RegisterComponent(this)
               .As<IWallZones>();
    }
}
```

Example: `client/Assets/GamePlay/Level/Elements/Services/WallZones.cs`. A MonoBehaviour that needs dependencies declares an `[Inject]` method (any name, parameters are resolved like constructor parameters):

```csharp
[Inject]
public void Construct(ISettings settings, IUpdater updater)
{
    _settings = settings;
    _updater = updater;
}
```

Example: `tools/framework/client/modules/audio/Runtime/AudioPlayer.cs`. Make the method `public` when the type is registered from another assembly: the generator cannot call an `internal` method of a foreign assembly (CINGR010). Add `.As<IScopeSetup>()` (and implement it) when the component needs setup.

**Prefab instance.** `builder.Instantiate(prefab)` instantiates and moves it to the scope scene; then `builder.Inject(instance)` and/or `builder.RegisterInstance(instance).As<...>()` (see `AddLoadingScreen` in `tools/framework/client/modules/ui/Runtime/UIExtensions.cs`).

New scene services: the compile-time check resolves scene services from `client/Assets/Common/Internal/Runtime/Tools/Container/ContainerGraph.AssetMap.cs`, written by `ContainerGraphPostprocessor` (menu `Tools/Framework/Codegen/Container Asset Map`). A new dependency on a scene service fails with CINGR003 until the component is in the scene, `Assets/Scan services` has run and the scene is saved. Add the dependency after that (see `CODEGEN.md`).

## Asset Groups

Catalogue groups are loaded through the scope builder and released with the scope:

```csharp
builder.RequestSpriteGroup(Sprites.Game.Players);     // retained before the container is built
builder.RequestPrefabGroup(GamePlayPrefabs.Group);
builder.RequestEnvAssetGroup(GamePlayAssets.Group);
builder.RequestAudioGroup(SfxAudio.Group);

await builder.LoadEnvAssetGroup(GlobalAssets.Group);  // loaded now: needed while registering
```

`Request*` defers loading to the before-build step; `Load*` awaits immediately and is used only when `Construct` itself reads the assets. Generated getters throw if the group is not loaded.

New asset of a catalogue: the getter (`InternalAssets.X`) exists only after the generator ran (`Tools/Framework/Codegen/Assets Catalog`, or Regenerate All), and the generator runs only in a project that compiles. So: the asset (group in its `.meta`), the asmdef reference of the content assembly to the assembly of its type (the generated class names the type), the generation, and only then the code that reads the getter. Code written first breaks the compile, and the generator cannot run until it is removed. Do not call `group.Retain()` from gameplay code; one lazy asset of a content bundle is `builder.RequestContent(reference, out var slot)` / `await builder.LoadContent(reference)` in a scope, or `IContentLoader.Load(lifetime, reference)` during the game (`CONTENT.md`). Code: `client/Assets/Common/Internal/Runtime/Catalogues/*/*Extensions.cs`, `.../Catalogues/Shared/AssetGroupBuilderExtensions.cs`.

## Runtime Access

`IContainer` (`Resolve<T>`, `TryResolve`, `ResolveAll<T>`, `Inject`) is for factories and tools; services take dependencies through constructors. `IViewInjector` (`GeneratedViewInjector`) injects runtime-created MonoBehaviours declared with `builder.Injectable<T>()`; injecting an unknown type throws `No generated inject for <type>`. The container is main-thread only (`ContainerThread.Assert`), and registering after the container is built throws.

## Diagnostics (CINGR) and Tools

The generator reports errors at compile time; the Unity console shows them as compiler errors.

| Id | Meaning | Typical fix |
|----|---------|-------------|
| CINGR001 | Walker does not understand a syntax form in an installer | Write the installer as plain calls of `BuilderExtensions` / other installers |
| CINGR001 `the services of scene 'Scenes.X'` | The scene of `FindOrLoadSceneWithServices` is not in the asset map this assembly sees. `sees no scene of the asset map`: the assembly walks scopes of the game without a reference to the content assembly | Reference the content assembly (`FrameworkSettings.ContentAssembly`) in the asmdef; when the map is visible, regenerate it (`Tools/Framework/Codegen/Container Asset Map`) or add the `SceneServicesFactory` to the scene |
| CINGR002 | Installer call cannot be resolved | Make the helper a non-generic extension method visible to the root assembly |
| CINGR003 | Constructor / `[Inject]` parameter has no registration in the scope or its parents | Register it (registration before the dependent type, see `CODEGEN.md` for scene services) |
| CINGR004 | Circular dependency (path in the message) | Break the cycle (event, lazy factory, split the service) |
| CINGR005 | Entity variant condition cannot be enumerated | Only `bool` / closed enum may change the graph; split the root |
| CINGR006 | One `ScopeEntityView` type used by two assets | One view subtype per prefab |
| CINGR007 | Declared parent scope manifest not found | Check `[ContainerScopeParent]` type and method name |
| CINGR008 | `builder.Injectable<T>` type has no visible `[Inject]` method | Add the method; `internal` methods of other assemblies are not visible |
| CINGR009 | Warning: a constructor / `[Inject]` parameter takes the last of several registrations of its type (`Resolve` gives the same one) | Take `IReadOnlyList<T>` to get all of them, or register one |
| CINGR010 | The `[Inject]` method cannot be called: several of them on the type and its base classes, generic, not accessible, `internal` or private in another assembly | Keep one non-generic `public` method |
| CINGR011 | The registered type cannot be constructed: struct, interface, abstract, static, not accessible, no accessible constructor, several constructors with the longest parameter count | Register a concrete class with one longest accessible constructor |
| CINGR013 | A scope root is overloaded: the runtime finds a root by its name only | Rename one of the methods |
| CINGR014 | Several `WithParameter` values of one registration fit one parameter and none has exactly its type | Pass one, or one of exactly the type of the parameter |
| CINGR015 | Warning: no parameter of the registered type can take a `WithParameter` value, it is not used | Remove the value or fix its type |
| CINGR016 | Scope root is generic: the method, a method around it or a type around it has type parameters | A non-generic root that calls a generic installer |
| CINGR017 | A lambda or an anonymous method is given as a scope root (`Func<IScopeBuilder, UniTask>`, `Func<IEntityBuilder, UniTask>`, the `construct` of the entity loader): the runtime finds the class by the name of the root method, every load would throw | A method group or a local function (it closes over the same values); one argument: `ScopeLoadOptions.Create(parent, Construct, arg)` |
| CINGR018 | A type implements `IScopeSetup` and no registration of it lists `IScopeSetup`: its `OnSetup` is never called | `.As<IScopeSetup>()` on the registration |

- `Tools/Container Debugger` (Play mode): live containers, registrations, resolve history, allocations per phase and per listener.
- `Tools/Container Graph` (Play mode): Graph Toolkit view of live scopes and their parent links.
- Generated sources can be inspected without Unity: `dotnet build client/<Asm>.csproj --no-incremental --no-dependencies -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=<dir>` (csproj files exist after Unity generated them).
- Allocation benchmarks of the container: `docs/roadmap/container-optimizations.md`.

## Common Mistakes

| Mistake | Result | Fix |
|---------|--------|-----|
| Lambda as scope root (`new ScopeLoadOptions(parent, b => ...)`, `LoadRoot(b => ...)`, the entity loader) | CINGR017 at compile time (a delegate built at runtime: `Scope root must be a method group or a local function`) | Pass the `Construct` method group or a local function; one argument: `ScopeLoadOptions.Create(parent, Construct, arg)` |
| Missing `[ContainerScopeParent]` on a child root | CINGR003 for every parent service | Declare the parent root |
| `if` choosing implementations inside one root | Both branches are built; alternative not registered at runtime breaks the scope | `switch` between roots; platforms: `#if`, or the branches inside one installer |
| `OnSetup` reads what the `OnSetup` of another service prepares | Works or not by the order of the registrations | Do it in the constructor of the other service, or call it in order where the scope is loaded |
| Initialization in a constructor / `Awake` that subscribes | No lifetime, leak | Subscribe in `OnSetup(lifetime)` |
| New scene service dependency before rescanning the scene | CINGR003 | Scan services, save scene, then add the dependency |

## Related
- **Lifetimes:** [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)
- **Reactive:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
- **Entities:** [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md), [HIERARCHY_BINDINGS.md](HIERARCHY_BINDINGS.md)
- **Code generation:** `CODEGEN.md` of the game
- **Code Style:** [CODE_STYLE_FULL.md](CODE_STYLE_FULL.md)
