# FORK: MemoryPack.Core for the WebGL client

A trimmed build of [MemoryPack](https://github.com/Cysharp/MemoryPack) 1.21.4 (MIT, see `LICENSE.md`). The client uses it instead of the DLL from NuGet. The backend still takes the full package from NuGet, the wire format is the same.

## Why

The NuGet version marks almost all of its formatters `[Preserve]` and registers them in the static constructor of `MemoryPackFormatterProvider`. So the Unity linker cannot strip them, and the WebGL build got System.Collections.Immutable, Brotli (the native encoder), BigInteger and System.Numerics, Uri, TimeZoneInfo, CultureInfo, Regex (through `TypeFormatter`) and tuples of up to 8 items. That is about 200 KB br of code and metadata. `link.xml` does not help here: it can only preserve, not remove `[Preserve]`.

The sources cannot go into Unity as they are: they need C# 11, and Unity compiles C# 9. So the DLL is built separately with the regular dotnet SDK.

## What is removed from the original

- `Compression/`: the Brotli compressor, decompressor and formatters. `BitPackFormatter` is kept. `BrotliStringFormatter` is removed from `StringFormatter.cs`, the Brotli attributes from `CustomFormatterAttributes.cs`.
- The formatters of BigInteger, BitArray, CultureInfo, TimeZoneInfo, Uri, Version, Type, StringBuilder, Immutable and Frozen collections, Lazy, Tuple/ValueTuple, multidimensional arrays.
- `MemoryPackFormatterProvider`: the registrations of these types and of System.Numerics (Complex, Vector*, Matrix*, Plane, Quaternion), the fallback path for Lazy, tuples, Immutable and multidimensional arrays.
- The `*.tt` templates (the generated `.cs` stay).
- The second round (the WebGL size on Unity 7000.0.0a20, −34 KiB br): the formatters of collections that no protocol of the games has (Stack, Queue, LinkedList, HashSet, SortedSet, PriorityQueue, Collection, ObservableCollection, Concurrent*, SortedDictionary, SortedList, ReadOnlyCollection, ReadOnlyObservableCollection, BlockingCollection), the interface formatters except `IReadOnlyList` and `IReadOnlyDictionary` (with the internal Grouping / Lookup), the array-like ones (`ArraySegment`, `Memory`, `ReadOnlyMemory`, `ReadOnlySequence`, `MemoryPool`, `ReadOnlyMemoryPool` and their attributes), and of the known types `decimal` and `DateTimeOffset`.

The protocol (`shared/`) uses only primitives, `string`, `Guid`, `DateTime`, `TimeSpan`, arrays, `List`, `Dictionary`, `IReadOnlyList`, `IReadOnlyDictionary`, `Nullable` and unions. If a type from the removed list is added to the protocol, Shared.Game does not compile in Unity (the generator finds no formatter) or the serialization throws `MemoryPackSerializationException` "not registered". Then bring the needed file back from the original repository.

## Build

```bash
client/packages/com.cysharp.memorypack/Source~/build.sh
```

The script puts the DLL into `Runtime/MemoryPack.Core.dll` of this package, the `.meta` stays the same. The project builds on its own (`ImportDirectoryBuildProps=false`, `ImportDirectoryPackagesProps=false`): `Directory.Build.props` and the central package versions of the framework break its build. To update MemoryPack: take `src/MemoryPack.Core` of the needed version, repeat the removals listed above and update `Analyzers/MemoryPack.Generator.dll` to the same version (the `RoslynAnalyzer` label in its `.meta` is required).

## Contents of the package

- `Runtime/MemoryPack.Core.dll` - the trimmed build (the result of `Source~/build.sh`).
- `Analyzers/MemoryPack.Generator.dll` - the generator from NuGet 1.21.4, unchanged.
- `Source~/` - the sources of the trimmed build. The history of the fork in git: the commit "Upstream MemoryPack.Core 1.21.4 source" is the original, the next one the removals.
