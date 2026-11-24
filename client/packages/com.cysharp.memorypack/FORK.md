# FORK: MemoryPack.Core для WebGL-клиента

Урезанная сборка [MemoryPack](https://github.com/Cysharp/MemoryPack) 1.21.4 (MIT, см. `LICENSE.md`). Клиент использует её вместо DLL из NuGet. Бэкенд по-прежнему берёт полный пакет из NuGet, формат на проводе тот же.

## Зачем

NuGet-версия помечает почти все свои форматтеры `[Preserve]` и регистрирует их в статическом конструкторе `MemoryPackFormatterProvider`. Поэтому Unity-линкер не может их вырезать, и в WebGL-билд попадали System.Collections.Immutable, Brotli (нативный энкодер), BigInteger и System.Numerics, Uri, TimeZoneInfo, CultureInfo, Regex (через `TypeFormatter`) и кортежи до 8 элементов. Это около 200 KB br кода и метаданных. `link.xml` тут не помогает: он умеет только сохранять, а не снимать `[Preserve]`.

Исходники нельзя положить прямо в Unity: они требуют C# 11, а Unity компилирует на C# 9. Поэтому DLL собирается отдельно обычным dotnet SDK.

## Что удалено относительно оригинала

- `Compression/`: Brotli-компрессор, декомпрессор и форматтеры. `BitPackFormatter` оставлен. Из `StringFormatter.cs` удалён `BrotliStringFormatter`, из `CustomFormatterAttributes.cs` — Brotli-атрибуты.
- Форматтеры BigInteger, BitArray, CultureInfo, TimeZoneInfo, Uri, Version, Type, StringBuilder, Immutable- и Frozen-коллекций, Lazy, Tuple/ValueTuple, многомерных массивов.
- `MemoryPackFormatterProvider`: регистрации этих типов и System.Numerics (Complex, Vector*, Matrix*, Plane, Quaternion), запасной путь для Lazy, кортежей, Immutable и многомерных массивов.
- Шаблоны `*.tt` (сгенерированные `.cs` остались).
- Второй круг (размер WebGL на Unity 7000.0.0a20, −34 KiB br): форматтеры коллекций, которых нет ни в одном протоколе игр (Stack, Queue, LinkedList, HashSet, SortedSet, PriorityQueue, Collection, ObservableCollection, Concurrent*, SortedDictionary, SortedList, ReadOnlyCollection, ReadOnlyObservableCollection, BlockingCollection), интерфейсные форматтеры кроме `IReadOnlyList` и `IReadOnlyDictionary` (с внутренними Grouping / Lookup), массивоподобные (`ArraySegment`, `Memory`, `ReadOnlyMemory`, `ReadOnlySequence`, `MemoryPool`, `ReadOnlyMemoryPool` и их атрибуты), из известных типов `decimal` и `DateTimeOffset`.

Протокол (`shared/`) использует только примитивы, `string`, `Guid`, `DateTime`, `TimeSpan`, массивы, `List`, `Dictionary`, `IReadOnlyList`, `IReadOnlyDictionary`, `Nullable` и юнионы. Если в протокол добавится тип из удалённого списка, Shared.Game в Unity не скомпилируется (форматтер не найден генератором) или сериализация бросит `MemoryPackSerializationException` «not registered». Тогда вернуть нужный файл из оригинального репозитория.

## Сборка

```bash
client/packages/com.cysharp.memorypack/Source~/build.sh
```

Скрипт кладёт DLL в `Runtime/MemoryPack.Core.dll` этого пакета, `.meta` остаётся прежним. Проект собирается сам по себе (`ImportDirectoryBuildProps=false`, `ImportDirectoryPackagesProps=false`): `Directory.Build.props` и центральные версии пакетов фреймворка ломают его сборку. При обновлении MemoryPack: взять `src/MemoryPack.Core` нужной версии, повторить удаления из списка выше и обновить `Analyzers/MemoryPack.Generator.dll` на ту же версию (метка `RoslynAnalyzer` в `.meta` обязательна).

## Состав пакета

- `Runtime/MemoryPack.Core.dll` - урезанная сборка (результат `Source~/build.sh`).
- `Analyzers/MemoryPack.Generator.dll` - генератор из NuGet 1.21.4 без изменений.
- `Source~/` - исходники урезанной сборки. История форка в git: коммит "Upstream MemoryPack.Core 1.21.4 source" - оригинал, следующий - удаления.
