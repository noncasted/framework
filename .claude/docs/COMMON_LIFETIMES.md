# Lifetimes: Resource Management Fundamentals

**TL;DR:** Lifetime = scope for subscriptions. When terminates → all children auto-terminate. **Rule: Every `Advise()`/`View()` MUST have Lifetime parameter or memory leak.**

## When to Use
Yes: All subscriptions (EventSource, ViewableProperty, ViewableList)
Yes: Resource cleanup, parent-child relationships
No: Static global data

## Core Concept
```
Parent Lifetime
├─ Child 1 (auto-terminates with parent)
└─ Child 2 (auto-terminates with parent)
```

## Creating Lifetimes

```csharp
// Standalone (manual cleanup)
var lifetime = new Lifetime();
lifetime.Terminate();

// Child (auto-cleanup with parent)
var child = parent.Child();

// From CancellationToken
var lifetime = token.ToLifetime();
```

## Key Rules

| Rule | Impact |
|------|--------|
| All Advise/View need Lifetime | Missing = memory leak |
| Child auto-terminates with parent | Automatic cleanup |
| Terminate() twice is safe | Idempotent |
| Listeners cleared after termination | No callbacks after death |
| `Listen(null)` throws `ArgumentNullException` | Fail fast, no silent no-op |
| Thread-safe (client and backend): `Listen`, `RemoveListener`, `Terminate`, `Token` from any thread | Listeners run on the thread that calls `Terminate`, outside the lock |
| `a.Intersect(b)` ends with either source; ended early (direct `Terminate`) it unsubscribes from both | No closures left on long-lived sources |
| `WaitInvoke` removes both subscriptions on completion and on cancellation | No leak on a cancelled wait |

## Common Pattern: Scoped Subscription

```csharp
public void OnSetup(IReadOnlyLifetime lifetime)
{
    _button.ListenClick(lifetime, OnButtonClicked);
    // Auto-cleaned when lifetime terminates
}
```

UI clicks of the game go only through `ListenClick(lifetime, ...)` (never `onClick.AddListener`), the pointer through
`UIHover.Entered` / `Exited` (`IEventSource`, `Advise(lifetime, ...)`): see the UI doc of the game.

## Common Mistakes

No: Forgot lifetime: `source.Advise(null, OnEvent)`
Yes: Always pass: `source.Advise(lifetime, OnEvent)`

## Quick Reference

**Creating:**
```
new Lifetime()
parentLifetime.Child()
token.ToLifetime()
```

**Using:**
```
lifetime.IsTerminated
lifetime.Terminate()
lifetime.Listen(() => { })
```

**Subscriptions:**
```
eventSource.Advise(lifetime, handler)
property.View(lifetime, handler)
```

## Gotchas & Edge Cases

### Listening to an Ended Lifetime

```csharp
var lt = new Lifetime();
lt.Terminate();

// Callbacks registered after termination fire IMMEDIATELY (the client Lifetime also logs an error)
lt.Listen(() => Debug.Log("Fires now!"));

// TerminatedLifetime is an already ended lifetime: the same, without the error log (F2.42).
// Its Token is cancelled; an Advise made with it is undone at once - nothing is delivered.
source.Advise(new TerminatedLifetime(), OnEvent);
```

**Why:** Cleanup already happened, so listener is invoked synchronously. Before F2.42 `TerminatedLifetime.Listen`
dropped the callback and its `Token` was `None`: an `Advise` with it stayed subscribed forever.

### Double Terminate

```csharp
var lt = new Lifetime();
lt.Terminate();
lt.Terminate();  // OK: Safe - idempotent operation
```

**Why:** Terminate is idempotent - second call is no-op.

### Timing: Callbacks After Terminate

```csharp
var lt = new Lifetime();
var sub = lt.Advise(lt, () => Debug.Log("Event"));
lt.Terminate();

sub.Invoke();  // WRONG: Callback does NOT fire
// Subscriptions already cleaned, Invoke has no effect
```

**Why:** All listeners are removed during Terminate.

### Child Lifetime After Parent Terminate

```csharp
var parent = new Lifetime();
var child = parent.Child();

parent.Terminate();
// child is also terminated (auto-cleanup)

child.Advise(child, OnEvent);  // WRONG: Fires immediately (child terminated)
```

**Why:** Child auto-terminates with parent.

### Circular Reference Risk

```csharp
// WRONG - lifetime captures this, this has lifetime
public class Service
{
    private Lifetime _lifetime;

    public void Setup()
    {
        _lifetime.Advise(_lifetime, () => {
            // this keeps _lifetime alive
        });
    }
    // GC can't reclaim Service because _lifetime has reference to callback
}

// CORRECT - use WeakReference if needed
public class Service
{
    private readonly WeakReference<Lifetime> _lifetime;
    // ...
}
```

**Why:** Callbacks captured by lifetime can prevent garbage collection.

## Lifetime Graph (editor-only leak finder)

`Tools/Lifetime Graph/Open` — live Graph Toolkit tree of every **alive** `Lifetime` created in Play Mode.

- Feed: `Lifetime` ctor / `Terminate()` call `LifetimeAnalytics` (all under `#if UNITY_EDITOR`). The ctor captures a `StackTrace`; node title = `Class:Method` of the first frame outside lifetime infrastructure (`Lifetime`, `LifetimeExtensions`, `ViewableList`, `ViewableDictionary`, `LifetimedValue`, `GameObjectLifetime*`; `System.*`/`Cysharp.*` frames are skipped). Lambdas/async/local functions unwrap to the source method (`Foo:OnSetup`, `Foo:OnSetup.Local`). Tooltip = first 12 frames.
- Parent link = the `parent` passed to `new Lifetime(parent)` / `Child()`. `new Lifetime()` (ViewableList/Dictionary items, `LifetimedValue` current value, roots) = root node. `Intersect` is created via `Lifetime.CreateIntersection(a, b)`: node hangs under `a`, dashed wire from `b` into its "Intersect" port (analytics-only parents, `_parent` stays null).
- "Collapse (N)" button on a node hides its whole subtree and re-layouts (subtitle shows "N hidden"); state is per graph instance, reset by Clear.
- Terminated lifetimes disappear from the graph. **Red node** = parent terminated, lifetime still alive (`new Lifetime(parent)` without `Child()` linkage). What survives a scene switch under a long-lived parent = leak candidate. After exiting Play the model keeps survivors.
- Tracking toggle: `Tools/Lifetime Graph/Track Lifetimes` (EditorPrefs, default on, only active in Play). Lifetimes created before tracking started have id 0 and are never shown.
- Code: `Runtime/Common/Reactive/Lifetimes/Analytics/LifetimeAnalytics.cs` (thread-safe queue), `Editor/Tools/LifetimeGraph/` (`LifetimeTracking` drains each editor update, `LifetimeLiveGraph` syncs incrementally every 0.5 s while the window is open).

## Next Steps
- **Patterns:** [COMMON_LIFETIMES_PATTERNS.md](COMMON_LIFETIMES_PATTERNS.md)
- **Reactive:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
- **DI:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
