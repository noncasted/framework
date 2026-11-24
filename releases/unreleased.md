# Ещё не в релизе

Шаги для игр из коммитов после последнего тега, формат в [README](README.md). Новый шаг дописывается в конец в том же коммите, что
и изменение. При релизе шаги переходят в `<версия>.md`, здесь остаётся только этот заголовок.

### MemoryPack клиента без форматтеров редких коллекций
Кого касается: клиент, типы протокола игры (`[MemoryPackable]` в `shared/` и в клиенте). На 2026-11-22 ни одна игра их не использует.
Поиск: `git grep -lE '\[MemoryPackable' -- '*.cs' ':!*/Tests/*' | xargs grep -nE '\b(HashSet|Queue|Stack|LinkedList|SortedSet|SortedDictionary|SortedList|PriorityQueue|Concurrent\w+|ObservableCollection|ReadOnlyCollection|BlockingCollection|ArraySegment|Memory|ReadOnlyMemory|ReadOnlySequence|IList|ICollection|IEnumerable|IReadOnlyCollection|ISet|IReadOnlySet|IDictionary|ILookup|IGrouping)<|\b(decimal|DateTimeOffset)\b'`
Считаются только сериализуемые члены: метод, который возвращает `IEnumerable<T>` (как `CollectElementIds` в storytale-workshop), не мешает.
Сделать: тип из списка в протоколе заменить на массив, `List`, `Dictionary`, `IReadOnlyList` или `IReadOnlyDictionary`. Иначе клиент
бросит `MemoryPackSerializationException` «not registered» при первой сериализации (бэкенд на полном MemoryPack из NuGet её не заметит).
Список удалённого: `client/packages/com.cysharp.memorypack/FORK.md`.
