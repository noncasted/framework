# API Design Patterns

## Rule 1: No Async Suffix for UniTask

`UniTask<T>` already signals async. Don't add `Async` suffix.

```csharp
// WRONG
public async UniTask<Character> LoadCharacterAsync(string id) { }

// CORRECT
public async UniTask<Character> LoadCharacter(string id) { }
```

## Rule 2: Return IReadOnlyList, Not Array

```csharp
// WRONG - client coupled to array
public Item[] GetInventory() => _items.ToArray();

// CORRECT - flexible interface
public IReadOnlyList<Item> GetInventory() => _items.AsReadOnly();
```

Benefits: Can swap internal impl later, client never breaks.

## Rule 3: Return Empty Collection, Not Null

```csharp
// WRONG - forces null checks
if (items != null) foreach (var item in items) { }

// CORRECT - no checks needed
public IReadOnlyList<Item> GetItems() => Array.Empty<Item>();
foreach (var item in GetItems()) { } // Works even if empty
```

## Rule 4: A File Error Throws

A missing or corrupted file is an unexpected state: it throws with the path. `catch` only where the failure is an
expected outcome the caller handles, and it states the reaction (`CODE_STYLE_FULL.md` § Errors). Resources made before the
failure are destroyed (`UnityEngine.Object.Destroy(texture)`).

```csharp
public Sprite LoadSprite(string path)
{
    var bytes = File.ReadAllBytes(path);
    var texture = new Texture2D(1, 1);

    if (texture.LoadImage(bytes) == false)
    {
        UnityEngine.Object.Destroy(texture);
        throw new InvalidDataException($"Not an image: {path}");
    }

    return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one / 2f);
}
```

## Rule 5: Wrap Callbacks with UniTask

Some APIs are callback-based, not async. Wrap them:

```csharp
public async UniTask<string> BrowseFile()
{
    string selectedPath = null;
    bool completed = false;

    FileBrowser.ShowLoadDialog(
        onSuccess: (paths) => {
            selectedPath = paths[0];
            completed = true;
        },
        onCancel: () => completed = true
    );

    while (!completed)
    {
        await UniTask.Delay(16); // Poll until ready (~60fps)
    }

    return selectedPath;
}
```

## Common Mistakes

No: Returning null for empty list
Yes: Return `Array.Empty<T>()`

No: Not cleaning up on file error
Yes: Destroy resources in catch block

No: Throwing on file error
Yes: Return null to allow batch loading

## Quick Reference

**Async:** `public async UniTask<T> MethodName()` (no Async suffix)
**Collections:** `public IReadOnlyList<T> GetItems()`
**Empty:** `return Array.Empty<T>()`
**File I/O:** throw with the path, destroy what was created
**Callbacks:** Poll with `UniTask.Delay(16)` until ready

## Related
- **Code Style:** [CODE_STYLE_FULL.md](CODE_STYLE_FULL.md)
- **Container:** [COMMON_CONTAINER.md](COMMON_CONTAINER.md)
