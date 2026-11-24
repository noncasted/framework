# ContainerGenerator

Roslyn incremental source generator (`ContainerGraphGenerator`) for the Internal container. It reads installer
methods at compile time and emits one concrete container class per scope root. There is no runtime reflection
fallback: a root without a generated class throws `No generated container for '<root>'` when the scope is built.

`Source~` is ignored by Unity. The built dll lives in `Analyzers/` of the core package and must stay
labeled `RoslynAnalyzer` with every platform disabled (see the `.meta`). Unity runs it for every assembly it compiles;
`dotnet build client/<Asm>.csproj` runs it too.

## Build / copy

From the framework root:

```bash
dotnet build client/core/Source~/ContainerGenerator/ContainerGenerator.csproj -c Release
```

The `CopyToAnalyzers` post-build target copies `ContainerGenerator.dll` into
`client/core/Analyzers/`; every Unity project using the framework picks it up on the next script compile. Do not copy `Microsoft.CodeAnalysis*.dll` next to it.
`-p:SkipAnalyzersCopy=true` (also for `dotnet run` of the harness, which builds the generator) builds without the copy:
a check next to a Unity that tests the same tree must use it. A new analyzer under a running batchmode test run requests a
script compilation; while it is pending `EditorApplication.isCompiling` stays true, the UniTask loop of the editor stops
and every test that awaits a frame or a timeout ends only by the 180 s timeout of the test framework.

Pin is `Microsoft.CodeAnalysis.CSharp` **4.3.0** (Unity-compatible 4.x), target `netstandard2.0`.

## Verify harness

```bash
dotnet run --project client/core/Source~/ContainerGenerator.Verify/ContainerGenerator.Verify.csproj -c Release
```

`ContainerGenerator.Verify` compiles stub `Internal` sources plus test installers in memory, runs the walker,
resolver, emitters and the full generator driver, and prints `PASS`/`FAIL` per case and `ALL PASSED`
(exit code 0) or `FAILED N` (exit code 1). Run it after every generator change. It reaches the generator internals
through `InternalsVisibleTo`.

## Pipeline

1. **Roots.** A root is a method marked `[ContainerGraphRoot]`, or a `Construct` / `Build` method with an
   `IScopeBuilder` / `IEntityBuilder` / `IBuilder` parameter. `[ContainerScopeParent(typeof(T), nameof(T.Method))]`
   names the parent scope (can sit on the root or on its containing method).
2. **Walk** (`Graph/Walker`, composed by `GraphWalker`). Follows installer calls from each root into one graph: registrations, `.As<>()`,
   `WithParameter`, markers, alternative `if/else` pairs, entity variants. Builder primitives are recognized by
   `[Internal.ContainerPrimitive(kind)]` on the method (`Graph/PrimitiveKind.cs` mirrors the runtime enum by value);
   an unbound call (code that does not compile yet) falls back to the method name. An `if` around a call into another
   installer is not carried inside that installer (both branches are built). A lifetime is the constant value of the
   `ServiceLifetime` parameter; a value that is not a constant is CINGR001, as is a transient `Register` in an arm of a
   switch expression. The id of a method is `Type.Method`; overloads of an installer also carry their parameter types.
   A root keeps the bare name, the runtime finds it by that: an overloaded root is CINGR013.
3. **Manifests** (`Graph/GraphEmitter`, `Graph/ManifestBlobFormat`, `Graph/ManifestBlobWriter`, `Scope/ManifestReader`,
   `Scope/ManifestBlobParser`, `Scope/ManifestHarvest`). Every assembly emits
   `ContainerGraph.g.cs` with `[assembly: Internal.ContainerInstaller(...)]` per installer method; consumer
   assemblies read them to resolve installers and parent scopes from other assemblies. A manifest of another
   `BlobVersion` or a broken one is CINGR001 naming the assembly: rebuild it with this generator.
4. **Entity assets** (`Entity/`). Prefab views and their bindings come from
   `[assembly: Internal.ContainerGraphAsset(...)]` in
   `<content folder>/Runtime/Tools/Container/ContainerGraph.AssetMap.cs` of the project (`FrameworkSettings`), written by the editor
   `ContainerGraphPostprocessor` (also scene services of `SceneServicesFactory`).
5. **Resolve** (`Scope/Resolve`, composed by `EdgeResolver`; `Scope/Plan`: the model `ScopePlan` and `ScopePlanBuilder`). Binds every constructor / `[Inject]` parameter to a
   registration, a parent export (typed hole), a collection, or a builder parameter; reports CINGR diagnostics.
6. **Emit** (`Scope/Emit`, composed by `ScopeEmitter`). One `{Namespace}.{Type}{Method}Container.g.cs` per root (and per entity variant; a nested type is `{Outer}_{Type}`):
   `internal sealed class {Type}{Method}Container : Internal.IContainer, Internal.IProvides<T>...` with a field per
   registration, ctor-time construction in dependency order, a static export table for `Resolve` / `TryResolve`
   (falls back to the parent), `Dispose`, and
   `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] RegisterGenerated()` calling
   `Internal.GeneratedScopes.Register(rootId, request => new ...Container(...), constructed, instances)`
   (or `RegisterVariant(rootId, typeof(View), ...)` for entity variants).
   Runtime values are bound to their registration, not to their type: a `RegisterInstance` / `RegisterComponent`
   slot takes `request.GetInstance<T>(n)` (the n-th instance registration of the declared type `T`, in installer
   order), a `WithParameter` hole takes `request.GetParameter<T>(typeof(Implementation), n)` (its own registration,
   then the parent), a parent export takes `request.GetFromParent<T>()`.
   `constructed` / `instances` list what the class has slots for; before building, the builder is checked against
   them: a registration the generator did not see (a call through an interface or a virtual method, a stale asset
   map) or more instances of a type than slots (a loop, a repeated installer call) throws.
   A parameter that can hold the `WithParameter` value of its own registration (the exact type first) takes that value before any registration
   of the type and before what the scope gives by itself (`IContainer`, the lifetimes); any other parameter must be
   registered in the scope or its parents.
   A type resolves to its last registration, the same one a constructor parameter takes (CINGR009 warns).
   Singleton alternatives that exclude each other (different arms of one `if` / `switch`) and share a service are one
   group with one field; two separate `if`s are two fields, both are built when both ran. The arm that got into the
   builder is built with its own `[Inject]`, `Dispose` checks the value at runtime, every service of every member is
   exported (a member that does not give the type gives nothing). A transient alternative keeps `IsRegistered` in a field.
   A branch that did not run gives nothing: the type takes the last of its registrations that ran; when none did, the
   scope still builds, a dependent throws (`RequireAlternative`), `Resolve` goes to the parent, lists leave it out.
   `Dispose` terminates the lifetime first (its listeners still see live services), then disposes the services in
   reverse construction order; a throwing step does not skip the rest, the errors are rethrown at the end; after it
   `Resolve` throws `ObjectDisposedException`. Transients are not tracked: whoever resolves one disposes it.
   A `builder.Inject(x)` slot also takes `request.GetInjections<T>()` (every instance passed with that type, e.g.
   identical scene services) and calls `[Inject]` on each of them in a loop; the slot field keeps the last instance.
   Several `builder.Inject` call sites of one type share a single loop.

At runtime `ScopeContainer` / `EntityScopeLoader` take the root id from the method group
(`GeneratedScopes.RootId`), and `GeneratedScopes.Create` invokes the registered factory with a
`GeneratedScopeRequest` (lifetime, parent, builder parameters, `IsRegistered` for alternatives).

`Injector/` holds the shared symbol lookups (`ReferenceSymbols`) and the per-type constructor / `[Inject]`
analysis (`TypeAnalyzer`, `InjectorModel`) used by the resolver. The `[Inject]` method is looked up on the type and
its base classes; an override counts as the method it overrides.

## Diagnostics

All are errors.

| Id | Meaning |
|----|---------|
| CINGR001 | Walker does not understand a syntax form in an installer (also wraps generator exceptions) |
| CINGR002 | Installer call cannot be resolved (e.g. a generic helper from another assembly) |
| CINGR003 | Constructor / `[Inject]` parameter has no registration in the scope or its parents; no class is emitted |
| CINGR004 | Circular dependency, with the path |
| CINGR005 | Entity variant condition cannot be enumerated (only `bool` and closed enums may change the graph) |
| CINGR006 | One `ScopeEntityView` type used by two assets |
| CINGR007 | Declared parent scope manifest not found |
| CINGR008 | `builder.Injectable<T>` type has no visible `[Inject]` method |
| CINGR009 | Warning: a parameter takes the last of several registrations of its type |
| CINGR010 | The `[Inject]` method cannot be called: several of them (the type and its base classes), generic, not accessible, internal or private in another assembly |
| CINGR011 | The registered type cannot be constructed: struct, interface, abstract, static, not accessible, no accessible constructor, several constructors with the longest parameter count |
| CINGR012 | Retired (0.14): there is one scope phase, a transient listener cannot get two |
| CINGR013 | A scope root is overloaded: the runtime finds a root by its name only |
| CINGR014 | Several `WithParameter` values of a registration fit one parameter and none has exactly its type |
| CINGR015 | Warning: a `WithParameter` value of a registration that no parameter can take |
| CINGR016 | A scope root is generic: the method, a method around it or a type around it has type parameters. A generic installer is fine |
| CINGR017 | A lambda or an anonymous method is given as a scope root: the runtime finds the class by the name of the root method |
| CINGR018 | A type implements `IScopeSetup` and no registration of it lists `IScopeSetup`: its `OnSetup` is never called |

An exception inside the generator is reported as CINGR001 for the scope it happened in; the other scopes of the
assembly are still emitted.

Check generated code without Unity:
`dotnet build client/<Asm>.csproj --no-incremental --no-dependencies -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=<dir>`.
