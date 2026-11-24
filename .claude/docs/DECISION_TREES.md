# Decision Trees: Choose Your Pattern

Fast lookup trees for architectural decisions. Use when unsure what pattern to apply.

---

## 1. Which Reactive Type Should I Use?

```
Need to send notification/signal?
├─ Yes, but no state persistence
│  ├─ Event without params? → EventSource<void> (or EventSource)
│  └─ Event with params? → EventSource<T> or EventSource<T1, T2, ...>
│
└─ No, need to store current state
   ├─ Single value (int, string, object)?
   │  ├─ Need initial callback now? → ViewableProperty.View()
   │  ├─ Need UI sync on change? → ViewableProperty.View()
   │  └─ Need only future changes? → ViewableProperty.Advise()
   │
   └─ Collection (list, set)?
      ├─ Need dynamic iteration (add/remove)? → ViewableList<T>
      ├─ Need key→value mapping? → ViewableDictionary<K, V>
      └─ Read-only view only? → IReadOnlyList<T>
```

**Quick Reference:**
- **EventSource** → notifications, one-time signals
- **ViewableProperty** → UI bindings, current state (healthText, ammoCount)
- **ViewableList** → dynamic collections (inventory, team, enemies)

---

## 2. Which Lifetime Should I Use?

```
Subscribing to something?
├─ In a lifecycle callback (OnSetup / OnLoaded / ... (lifetime))?
│  └─ Use parameter lifetime directly
│     onEvent.Advise(lifetime, callback)
│
├─ In a regular class constructor?
│  ├─ Is it a service (registered in container)? → Do not subscribe in the constructor:
│  │     register .As<IScopeSetup>() and subscribe in OnSetup(lifetime)
│  │     (or .WithScopeLifetime() to receive IReadOnlyLifetime as a constructor parameter)
│  └─ Is it not a service? → Take a lifetime from the caller
│
├─ Listening to an item in a collection?
│  ├─ Item has its own .Lifetime? → Use item.Lifetime
│  │  items.View(sceneLifetime, item => {
│  │      item.Events.Advise(item.Lifetime, ...)
│  │  })
│  │
│  └─ Item doesn't have .Lifetime? → Create child lifetime
│     items.View(sceneLifetime, item => {
│         var itemLifetime = sceneLifetime.Child()
│         item.Events.Advise(itemLifetime, ...)
│     })
│
└─ Wrapping a callback (file dialog, async operation)?
   └─ Create scoped lifetime
      var lifetime = new Lifetime()
      return UniTask.Create(async () => { ... lifetime.Terminate() })
```

**Quick Reference:**
- **OnSetup(lifetime) / other phase** → use parameter
- **Service class** → subscribe in OnSetup(lifetime); `.WithScopeLifetime()` if the constructor needs it
- **Collection item** → item.Lifetime if exists, else Child()
- **One-shot operation** → new Lifetime() then Terminate()

---

## 3. How Should I Register a Service?

```
Is it a Unity object (lives in a scene or prefab)?
├─ No (game logic, UI logic, state, factory)?
│  └─ Plain class, constructor injection
│     builder.Register<MyService>().As<IMyService>()
│     needs setup? → also .As<IScopeSetup>(), subscribe in OnSetup(lifetime)
│
├─ Scene object (service placed in a scene with SceneServicesFactory)?
│  └─ MonoBehaviour, ISceneService
│     Create(IScopeBuilder builder) → builder.RegisterComponent(this).As<IMyService>()
│     dependencies → [Inject] method; setup → also .As<IScopeSetup>()
│     then Assets/Scan services + save scene (see CODEGEN.md)
│
├─ Prefab instantiated by a service scope (loading screen, overlay)?
│  └─ var view = builder.Instantiate(prefab)
│     builder.Inject(view);  builder.RegisterInstance(view).As<IMyView>()
│
├─ Part of a gameplay entity (player, weapon, enemy prefab)?
│  └─ Generated *Bindings (IEntityComponent) on the prefab + ScopeEntityView subclass
│     components are plain classes taking the bindings in the constructor (GAMEPLAY_ARCHITECTURE.md § Entity)
│
└─ MonoBehaviour created at runtime that needs dependencies?
   └─ builder.Injectable<T>() in the scope + IViewInjector.Inject(instance)
```

**Quick Reference:**
- **Logic** → plain class + `Register<T>().As<I>()`
- **Scene object** → `ISceneService` + `RegisterComponent` + `[Inject]` method
- **Entity prefab** → generated bindings + plain components
- **Runtime MonoBehaviour** → `Injectable<T>()` + `IViewInjector`

---

## 4. What Return Type Should I Use?

```
Method does work and returns something?
├─ Async work?
│  ├─ Returns result (string, Item, IReadOnlyList)?
│  │  └─ UniTask<T>                            (NOT UniTask<T>Async)
│  │     public async UniTask<Character> LoadCharacter(string id)
│  │
│  └─ No result (just waiting)?
│     └─ UniTask
│        public async UniTask WaitForAnimation()
│
├─ Synchronous, returns a collection?
│  ├─ Can be empty? → IReadOnlyList<T>         (never null)
│  │  public IReadOnlyList<Item> GetInventory() => _items.AsReadOnly()
│  │
│  └─ Always has items? → IReadOnlyList<T>     (still safe)
│     public IReadOnlyList<Ability> GetAbilities() => _abilities.AsReadOnly()
│
└─ Synchronous, returns single object?
   ├─ Can fail/not exist? → T? (nullable)
   │  public Character? FindCharacter(string id) => _characters.TryGetValue(id, ...)
   │
   └─ Must exist? → T (non-nullable)
      public Character GetActive() => _currentCharacter
```

**Quick Reference:**
- **Async + result** → UniTask<T>
- **Async no result** → UniTask
- **Collection** → IReadOnlyList<T> (never null)
- **Single object, optional** → T?
- **Single object, required** → T

---

## 5. UI Binding: Which Method to Use?

```
Binding a value to UI text/image/etc?
├─ Show current value NOW + listen for changes?
│  └─ Use View()
│     _health.View(lifetime, hp => {
│         healthText.text = $"HP: {hp}"
│     })
│     // First callback fires immediately with current value
│
└─ Only listen for FUTURE changes?
   └─ Use Advise()
      _health.Advise(lifetime, hp => {
          healthText.text = $"HP: {hp}"
      })
      // First callback fires on next change, not current value

GOLDEN RULE: 95% of UI bindings need View(), not Advise()
```

**Quick Reference:**
- **UI display** → View() (shows current + future)
- **Event notifications** → Advise() (future only)

---

## 6. Collection Item Subscription: Lifetime Strategy

```
Listening to events from items in a collection?

items.View(lifetime, (entryLifetime, item) => {
    item.Events.Advise(entryLifetime, OnItemEvent)
    // entryLifetime ends with the removal of the item or with lifetime
})

WRONG - MEMORY LEAK:
items.View(lifetime, item => {
    item.Events.Advise(lifetime, ...)  // Persists after item removal!
})
```

**Quick Reference:**
- **Subscription to an item** -> `entryLifetime` of `View`, or `item.Lifetime` when the item has its own

---

## 7. File I/O & Error Handling

```
Can this operation fail?
├─ Is the failure an unexpected state?
│  └─ Throw. No catch, no null, no default.
│
└─ Is it an expected outcome the caller handles (network, a file that may be absent)?
   └─ Catch at this boundary only, log, state the reaction.
```

**Quick Reference:** `CODE_STYLE_FULL.md` § Errors.

---

## 8. When to Use NoAwait Pattern

```
Do you need the result, the completion or the exception here?
├─ YES -> await
└─ NO  -> task.NoAwait()  (never .Forget(), never async void)
```

---

## Usage: When Unsure

1. Search this file for your scenario
2. Follow the tree to narrow down
3. Use the quick reference
4. If still unsure → check COMMON_*.md for full examples

For more context:
→ [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md) - Lifetime details
→ [COMMON_REACTIVE_BASICS.md](COMMON_REACTIVE_BASICS.md) - EventSource details
→ [COMMON_REACTIVE_VALUES.md](COMMON_REACTIVE_VALUES.md) - ViewableProperty details
→ [COMMON_REACTIVE_COLLECTIONS.md](COMMON_REACTIVE_COLLECTIONS.md) - ViewableList details
→ [API_DESIGN_FULL.md](API_DESIGN_FULL.md) - Return types & method design
→ [COMMON_CONTAINER.md](COMMON_CONTAINER.md) - Container, scopes, registration
