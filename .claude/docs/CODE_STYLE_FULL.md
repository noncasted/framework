# Code Style Guide

Rules for all code of the framework and the games. `/code-audit` checks them.

## Member Order

```csharp
public class MyService : IMyService, IScopeSetup
{
    // 1. Constructor
    public MyService(IDependency dependency)
    {
        _dependency = dependency;
    }

    // 2. Private fields - readonly first, then mutable
    private readonly IDependency _dependency;
    private readonly EventSource<int> _onEvent = new();
    private int _counter;

    // 3. Public members
    public void OnSetup(IReadOnlyLifetime lifetime)
    {
    }

    // 4. Private methods
    private void Helper()
    {
    }
}
```

Local functions stand at the end of their method.

## Naming

Private fields are `_camelCase`. Names are descriptive, without abbreviations.

```csharp
// CORRECT
private int _health;
private List<Ability> _abilities;
private EventSource<int> _onDamaged = new();

// WRONG
private int _hp;
private List<Ability> _ab;
private EventSource<int> _damage;
```

## Braces: On a New Line

The `.editorconfig` of the projects sets the same. An `if` may go without braces when its body is one short statement.

```csharp
public class MyService
{
    public void Method()
    {
        if (condition)
        {
            DoSomething();
        }
    }
}
```

## Comments

A comment explains what the code cannot say: a reason, a constraint, a trap. It is in English and uses `//`. No `///` XML docs
and no retelling of the code.

## Data Changes Only Through Methods

There are no public setters and no public mutable fields anywhere: services, entities, ECS components, Orleans states,
tests and benchmarks. State is changed by intent methods (`Init`, `Tick(delta)`, `Restart`, `Hit`), properties are
`{ get; private set; }`; a value fixed after creation is `readonly`, `{ get; }` or `{ get; init; }`. A Unity field shown in
the inspector is `[SerializeField] private`.

The rule counts a `public` member of an `internal` type too: it is visible to the whole assembly.

`init` needs `IsExternalInit`, which Unity's profile does not have: code compiled by Unity takes the values in a
constructor (optional named arguments for a style of many values) and does not add a polyfill.

The exceptions, the only ones:
- a `[MemoryPackable]` type (network payloads and contexts): its public setters are allowed;
- a `[Serializable]` type without logic that only `JsonUtility` reads and writes (a trace file, `SessionState` data): the
  names of its public fields are the format of the file, and `JsonUtility` sees neither properties nor `init`;
- a Blazor `[Parameter]`: the framework throws on a non-public setter (`[Inject]` needs none, it stays non-public);
- a named argument of an attribute: C# requires a public setter;
- an `override` of a public property of a foreign type.

A stored Orleans state needs `[JsonProperty]` on every `{ get; private set; }` property: Newtonsoft does not write a
private setter without it, so the value is saved but silently comes back as the default on load. `{ get; init; }` needs
no attribute. See `COMMON_ORLEANS.md`.

## Size and Files

- A file of runtime code or of an editor tool above 300 lines is split, or the audit report says why it stays. Generated
  code and tests have no limit.
- One top-level type per file, the file is named after it. Allowed together: `IX` + `X`, `X` + `XExtensions`.

## Async

- A method returning `UniTask` / `Task` has no `Async` suffix, on the client and the backend. The exception is an override or
  an implementation of a foreign API (`OnActivateAsync`).
- A task that is not awaited ends with `.NoAwait()`, never `.Forget()`.
- No `async void`.

```csharp
public void StartAnimation()
{
    PlayAnimation().NoAwait();
}

private async UniTask PlayAnimation()
{
    await _animator.Play(_clip);
}
```

## Errors: Fail Fast, Catch at Boundaries

An unexpected state throws, and the exception goes up. There are no defensive null checks and no silent fallbacks.

`catch` stands only at a boundary, where the code can really react:

- network and files, when a failure is an expected outcome the caller handles;
- the top of a task that nobody awaits;
- the isolation of subscribers (`ReactiveExceptions`) and of updatables;
- a user-facing error of the console (`ToastService`);
- `OperationCanceledException` of a wait, when the end of its lifetime is the expected path.

A `catch` logs the exception or passes it on, and states its reaction. It is never empty and never returns `null` or a default to hide
the failure. Graceful degradation appears only when the user asked for it.

## Collections: Initialize Inline

```csharp
// CORRECT
private readonly List<Item> _items = new();

// WRONG
private List<Item> _items;
```

## Dictionary Lookup: TryGetValue

```csharp
// WRONG - two lookups
if (_map.ContainsKey(key))
{
    var value = _map[key];
}

// CORRECT - one lookup
if (_map.TryGetValue(key, out var value))
{
}
```

## Local Functions

Use them for closures over the variables of a method and for helpers needed only there.

## ECS Components

A StaticEcs component (`IComponent`) follows "Data Changes Only Through Methods". Create by
`body.Add<T>().Init(...)`; an immutable one is a `readonly struct` with a constructor (`body.Set(new Body(id, kind))`). Call
a changing method only on a ref (`body.Ref<T>().Tick(delta)`, `ref var x = ref body.Ref<T>()`), never on a copy
(`var x = body.Ref<T>()`); reading members are `readonly`; never name a method `OnAdd`/`OnDelete`/`CopyTo`/`Write`/`Read`
(hooks of the package). Details: the ECS doc of the game.

## Related
- **API Design:** [API_DESIGN_FULL.md](API_DESIGN_FULL.md)
- **Container:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
