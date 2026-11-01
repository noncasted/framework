# Reactive: Events & EventSource

**TL;DR:** EventSource = fire-and-forget event. Publisher invokes → listeners notified. Always with Lifetime.

## When to Use
Yes: Broadcasting notifications (clicked, changed, completed)
Yes: One-way signals
No: Storing state (use ViewableProperty)
No: Collections (use ViewableList)

## EventSource Variants

```csharp
// No params
new EventSource()
_onGameStarted.Invoke()
_onGameStarted.Advise(lifetime, () => { })

// With param
new EventSource<int>()
_onHealthChanged.Invoke(100)
_onHealthChanged.Advise(lifetime, health => { })

// Multiple params
new EventSource<string, float>()
_onCurrencyChanged.Invoke("gold", 100f)
```

## ViewableDelegate (Named Event Wrapper)

```csharp
private readonly ViewableDelegate<string> _onChat = new();
public IViewableDelegate<string> OnChat => _onChat;

_onChat.Invoke("hello");
_onChat.Advise(lifetime, msg => { });
```

## Key Rules

| Rule | Impact |
|------|--------|
| All Advise need Lifetime | Memory leak if missing |
| Fire = immediate callback | Synchronous notification |
| Register after fire = miss event | Event fires once |
| Expose as read-only | Client can't invoke |
| Each `Advise` is its own subscription | The same handler advised twice is invoked twice; the end of one lifetime removes exactly its own subscription |
| `Advise(lifetime, null)` throws | Fail fast |
| `ViewableProperty<T>` always has `ValueLifetime` | Also for a default value type start (`new ViewableProperty<bool>(false)`) |

## Common Pattern: Publisher Service

```csharp
// plain service: builder.Register<DamageSystem>().As<IDamageSystem>()
public class DamageSystem : IDamageSystem
{
    private readonly EventSource<int, int> _onDamage = new();
    public IEventSource<int, int> OnDamage => _onDamage;

    public void DealDamage(int targetId, int amount)
    {
        _onDamage.Invoke(targetId, amount);
    }
}
```

## Common Mistakes

No: Forgot lifetime: `eventSource.Advise(null, OnEvent)`
Yes: Always pass: `eventSource.Advise(lifetime, OnEvent)`

No: Storing value in EventSource (latecomers miss it)
Yes: Use ViewableProperty for state

## Quick Reference

**Creating:**
```
new EventSource()
new EventSource<T>()
new EventSource<T1, T2>()
new ViewableDelegate<T>()
```

**Using:**
```
eventSource.Invoke(value)
eventSource.Advise(lifetime, handler)
public IEventSource OnEvent => _onEvent;
```

## Next Steps
- **Values:** [COMMON_REACTIVE_VALUES.md](COMMON_REACTIVE_VALUES.md)
- **Collections:** [COMMON_REACTIVE_COLLECTIONS.md](COMMON_REACTIVE_COLLECTIONS.md)
- **Patterns:** [COMMON_REACTIVE_PATTERNS.md](COMMON_REACTIVE_PATTERNS.md)
