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
### Быстрый поиск: очередь игры `Queue` (BREAKING)

Кого касается: бэкенд игр с поиском фреймворка (`ISessionSearch`, `ISessionRequester`: pixel-swarm, neon-dodge; у mines-leader и
peace-data-reborn свои `ISessionSearch` - пропустить) и клиент игр с модулем `lobbies` (`IMatchmaking`: neon-dodge; у pixel-swarm и
peace-data-reborn свой `IMatchmaking`). Поиск несёт целое `queue` от клиента до поиска игры: матчи разных очередей не смешиваются, что
значит очередь (режим, карта) - решает игра; 0 - по умолчанию.
Поиск: `git grep -nE "ISessionSearch|GetOrCreate\(|ISessionRequester|\.Request\(session|SearchSession" -- backend client/Assets` и
`git grep -n "SharedSessionRequests+Search" -- backend`.
Сделать:
- `ISessionSearch.GetOrCreate(Guid userId, out bool isLobby)` -> `GetOrCreate(Guid userId, int queue, out bool isLobby)`: реализация
  игры (`backend/Game/Global/SessionSearch.cs`), её вызовы и фейки в тестах; игре без очередей - игнорировать `queue` (приходит 0).
- `ISessionRequester.Request(Guid userId)` -> `Request(Guid userId, int queue)`: команда поиска меты передаёт
  `SharedSessionRequests.Search.Queue` (`SearchSessionCommand` / `SessionCommands`), подмены `ISessionRequester` в тестах
  (`requester.Request(id)` -> `requester.Request(id, 0)` / `Arg.Any<int>()`).
- `IMatchmaking.SearchSession(IReadOnlyLifetime lifetime, int queue = 0)`: прямые вызовы не меняются; группа методов
  (`_matchmaking.SearchSession` как `Func<IReadOnlyLifetime, ...>`) с необязательным параметром не совпадает - лямбда
  `l => _matchmaking.SearchSession(l)`; свои реализации и фейки `IMatchmaking` (`FakeMatchmaking`) - новая подпись.
- Новый член `SharedSessionRequests.Search.Queue` (`int`): золотые файлы протокола игры (`protocol_schema.golden.txt`,
  `protocol_bytes.golden.txt`, строка `Shared.SharedSessionRequests+Search`) - перезаписать `UPDATE_PROTOCOL_GOLDEN=1`, хеш протокола
  игры (`ProtocolHash`) - поднять: формат `Search` на проводе другой.
