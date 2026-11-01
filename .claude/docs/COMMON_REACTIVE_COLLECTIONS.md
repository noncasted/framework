# Reactive: Collections & Observables

**TL;DR:** ViewableList = observable collection. Add item → listener notified + item gets lifetime. Remove item → lifetime terminates.

## When to Use
Yes: Observable collections (inventory, teams, enemies)
Yes: Each item has lifetime (cleanup on remove)
Yes: Dynamic UI lists
No: Static lists (just use List<T>)

## ViewableList

```csharp
var items = new ViewableList<Item>();

// Add (returns item's lifetime)
var itemLt = items.Add(sword);

// View = iterate existing + listen for new
items.View(lifetime, item => {
    Debug.Log($"Added: {item.Name}");
});

// Remove (terminates item's lifetime)
items.Remove(sword);
```

## ViewableDictionary

```csharp
var stats = new ViewableDictionary<string, int>();

stats.Add("health", 100);
stats.Add("mana", 50);

// Safe lookup
if (stats.TryGetValue("health", out var hp))
{
    Debug.Log(hp);
}

stats.View(lifetime, (key, value) => {
    Debug.Log($"{key}: {value}");
});

// Auto-remove on scope end
stats.AddLifetimed(scopeLifetime, "buff", 1);
```

## Key Rules

| Rule | Impact |
|------|--------|
| Each item has lifetime | Auto-cleanup when removed |
| View iterates existing + listens | UI sees all items immediately |
| Advise listens for future only | Existing items missed |
| Use item.Lifetime for subscriptions | Auto-cleanup when item removed |
| `View(lifetime, (entryLifetime, item) => ...)`: `entryLifetime` ends with the removal OR with `lifetime` | No manual `entryLifetime.Intersect(lifetime)` needed (F2.42); a listener on it also runs when the subscriber ends - guard side effects meant only for a removal with `lifetime.IsTerminated` |
| Remove of a missing key/item throws, a removed key can be added again | Lifetime map entry leaves with the item (F2.42 follow-up) |

## Common Pattern: UI List

```csharp
public void OnSetup(IReadOnlyLifetime lifetime)
{
    _inventory.Items.View(lifetime, item => {
        var itemUI = CreateUI(item);

        // Auto-destroy when item removed
        item.Lifetime.Listen(() => {
            Destroy(itemUI.gameObject);
        });
    });
}
```

## Common Pattern: Item-Scoped Subscription

```csharp
// WRONG - subscription leaks when item removed
_items.View(sceneLifetime, item => {
    item.Events.Advise(sceneLifetime, OnEvent);
});

// CORRECT - subscription cleaned when item removed
_items.View(sceneLifetime, item => {
    item.Events.Advise(item.Lifetime, OnEvent);
});
```

## Common Mistakes

No: Using Advise instead of View: `items.Advise(lt, item => { })`
   (existing items not shown in UI)
Yes: Use View: `items.View(lt, item => { })`

No: Subscribing with wrong lifetime: `item.Events.Advise(sceneLifetime, ...)`
   (subscription leaks when item removed)
Yes: Use item lifetime: `item.Events.Advise(item.Lifetime, ...)`

## Quick Reference

**ViewableList:**
```
new ViewableList<T>()
list.Add(item)                          // Returns item lifetime
list.Remove(item)                       // Terminates item lifetime + drops internal lifetime key
list.NotifyChangedAt(index)             // Inplace field update — does not recreate item lifetime
list.View(lifetime, handler)            // Iterate existing + listen
list.Advise(lifetime, handler)          // Listen for future only
```

**ViewableDictionary:**
```
new ViewableDictionary<K,V>()
dict.Add(key, value)
dict.TryGetValue(key, out value)
dict.Remove(key)
dict.AddLifetimed(scopeLt, key, value)  // Auto-remove
```

## Gotchas & Edge Cases

### Inplace Update — Do Not RemoveAt + Add

Updating a field on an existing item (e.g. `TurnsToEnd` on a modifier overview) must not `RemoveAt` + `Add`. That terminates the item lifetime, recreates UI, and can throw if `Remove` does not drop the internal lifetime key before the next `Add`.

```csharp
// WRONG — flicker + ArgumentException on re-add
overviews.RemoveAt(index);
overviews.Add(updated);

// CORRECT — mutate fields, then notify
existing.TurnsToEnd = next;
overviews.NotifyChangedAt(index);
```

### Remove During View Iteration

```csharp
var items = new ViewableList<Item>();
items.Add(sword);
items.Add(shield);

items.View(lifetime, item => {
    items.Remove(item);  // WARNING: Removing during iteration
});
// → Results in undefined behavior (may skip items or crash)
```

**Why:** Modifying collection during iteration breaks enumerator.

**Fix:**
```csharp
items.View(lifetime, item => {
    // Just mark for removal
    item.IsMarkedForRemoval = true;
});

// Remove marked items later
var toRemove = items.Where(i => i.IsMarkedForRemoval).ToList();
foreach (var item in toRemove)
{
    items.Remove(item);
}
```

### Item Lifetime Timing

```csharp
var items = new ViewableList<Item>();
var itemLt = items.Add(sword);

// Item lifetime is alive
Debug.Log(itemLt.IsTerminated);  // false

items.Remove(sword);
// Item lifetime auto-terminates
Debug.Log(itemLt.IsTerminated);  // true
```

**Why:** Item's lifetime is controlled by the collection.

### Advise vs View on Empty List

```csharp
var items = new ViewableList<Item>();

// Advise on empty list - callback never fires for existing items (none)
items.Advise(lifetime, item => Debug.Log("Added"));

// Add now - fires once
items.Add(sword);  // Prints "Added"

// View on existing items - callback fires for all existing
items.View(lifetime, item => Debug.Log("Exists"));
// Prints "Exists" for sword immediately
```

**Why:** Advise waits for new items, View includes existing.

### ViewableDictionary TryGetValue

```csharp
var stats = new ViewableDictionary<string, int>();
stats.Add("health", 100);

// OK: Safe lookup
if (stats.TryGetValue("health", out var value))
{
    Debug.Log(value);  // 100
}

// WRONG: Unsafe - throws if missing
var value2 = stats["health"];  // OK, exists
var value3 = stats["mana"];    // KeyNotFoundException
```

**Why:** Dictionary accessor throws, TryGetValue is safe.

### AddLifetimed vs Manual Remove

```csharp
var stats = new ViewableDictionary<string, int>();
var scopeLt = new Lifetime();

// OK: Auto-removes when scope ends
stats.AddLifetimed(scopeLt, "buff", 1);

// Manual approach
stats.Add("debuff", -1);
scopeLt.Listen(() => stats.Remove("debuff"));

scopeLt.Terminate();
// Both "buff" and "debuff" are removed
```

**Why:** AddLifetimed auto-removes on lifetime termination.

### Item View Callback Fires Immediately

```csharp
var items = new ViewableList<Item>();
items.Add(sword);
items.Add(shield);

// View with 2 existing items - callback fires 2 times immediately
items.View(lifetime, item => {
    Debug.Log($"Item: {item.Name}");
});
// Prints: "Item: Sword", "Item: Shield" synchronously

// Then for future adds
items.Add(dagger);
// Prints: "Item: Dagger"
```

**Why:** View includes existing items in the initial scan.

## Next Steps
- **Events:** [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md)
- **Values:** [COMMON_REACTIVE_VALUES.md](COMMON_REACTIVE_VALUES.md)
- **Patterns:** [COMMON_REACTIVE_PATTERNS.md](COMMON_REACTIVE_PATTERNS.md)
