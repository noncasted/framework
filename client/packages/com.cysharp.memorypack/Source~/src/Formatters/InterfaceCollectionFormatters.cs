using MemoryPack.Formatters;
using MemoryPack.Internal;
using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Interface collection formatters of this build: IReadOnlyList, IReadOnlyDictionary (the others of the original are removed, see FORK.md)

namespace MemoryPack
{
    public static partial class MemoryPackFormatterProvider
    {
        static readonly Dictionary<Type, Type> InterfaceCollectionFormatters = new(2)
        {
            { typeof(IReadOnlyList<>), typeof(InterfaceReadOnlyListFormatter<>) },
            { typeof(IReadOnlyDictionary<,>), typeof(InterfaceReadOnlyDictionaryFormatter<,>) },
        };
    }
}

namespace MemoryPack.Formatters
{
    using static InterfaceCollectionFormatterUtils;

    file static class InterfaceCollectionFormatterUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySerializeOptimized<TBufferWriter, TCollection, TElement>(ref MemoryPackWriter<TBufferWriter> writer, [NotNullWhen(false)] scoped ref TCollection? value)
            where TCollection : IEnumerable<TElement>
#if NET7_0_OR_GREATER
            where TBufferWriter : IBufferWriter<byte>
#else
            where TBufferWriter : class, IBufferWriter<byte>
#endif
        {
            if (value == null)
            {
                writer.WriteNullCollectionHeader();
                return true;
            }

            // optimize for list or array

            if (value is TElement?[] array)
            {
                writer.WriteArray(array);
                return true;
            }

#if NET7_0_OR_GREATER
            if (value is List<TElement?> list)
            {
                writer.WriteSpan(CollectionsMarshal.AsSpan(list));
                return true;
            }
#endif

            return false;
        }

        public static void SerializeCollection<TBufferWriter, TCollection, TElement>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref TCollection? value)
            where TCollection : ICollection<TElement>
#if NET7_0_OR_GREATER
            where TBufferWriter : IBufferWriter<byte>
#else
            where TBufferWriter : class, IBufferWriter<byte>
#endif
        {
            if (TrySerializeOptimized<TBufferWriter, TCollection, TElement>(ref writer, ref value)) return;

            var formatter = writer.GetFormatter<TElement>();
            writer.WriteCollectionHeader(value.Count);
            foreach (var item in value)
            {
                var v = item;
                formatter.Serialize(ref writer, ref v!);
            }
        }

        public static void SerializeReadOnlyCollection<TBufferWriter, TCollection, TElement>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref TCollection? value)
            where TCollection : IReadOnlyCollection<TElement>
#if NET7_0_OR_GREATER
            where TBufferWriter : IBufferWriter<byte>
#else
            where TBufferWriter : class, IBufferWriter<byte>
#endif
        {
            if (TrySerializeOptimized<TBufferWriter, TCollection, TElement>(ref writer, ref value)) return;

            var formatter = writer.GetFormatter<TElement>();
            writer.WriteCollectionHeader(value.Count);
            foreach (var item in value)
            {
                var v = item;
                formatter.Serialize(ref writer, ref v!);
            }
        }

        public static List<T?>? ReadList<T>(ref MemoryPackReader reader)
        {
            var formatter = reader.GetFormatter<List<T?>>();
            List<T?>? v = default;
            formatter.Deserialize(ref reader, ref v);
            return v;
        }
    }

    [Preserve]
    public sealed class InterfaceReadOnlyListFormatter<T> : MemoryPackFormatter<IReadOnlyList<T?>>
    {
        static InterfaceReadOnlyListFormatter()
        {
            if (!MemoryPackFormatterProvider.IsRegistered<List<T?>>())
            {
                MemoryPackFormatterProvider.Register(new ListFormatter<T>());
            }
        }

        [Preserve]
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref IReadOnlyList<T?>? value)
        {
            SerializeReadOnlyCollection<TBufferWriter, IReadOnlyList<T?>, T?>(ref writer, ref value);
        }

        [Preserve]
        public override void Deserialize(ref MemoryPackReader reader, scoped ref IReadOnlyList<T?>? value)
        {
            value = ReadList<T?>(ref reader);
        }
    }

    [Preserve]
    public sealed class InterfaceReadOnlyDictionaryFormatter<TKey, TValue> : MemoryPackFormatter<IReadOnlyDictionary<TKey, TValue?>>
        where TKey : notnull
    {
        readonly IEqualityComparer<TKey>? equalityComparer;

        public InterfaceReadOnlyDictionaryFormatter()
            : this(null)
        {

        }

        public InterfaceReadOnlyDictionaryFormatter(IEqualityComparer<TKey>? equalityComparer)
        {
            this.equalityComparer = equalityComparer;
        }

        [Preserve]
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref IReadOnlyDictionary<TKey, TValue?>? value)
        {
            if (value == null)
            {
                writer.WriteNullCollectionHeader();
                return;
            }

            var keyFormatter = writer.GetFormatter<TKey>();
            var valueFormatter = writer.GetFormatter<TValue>();

            writer.WriteCollectionHeader(value.Count);
            foreach (var item in value)
            {
                KeyValuePairFormatter.Serialize(keyFormatter, valueFormatter, ref writer, item!);
            }
        }

        [Preserve]
        public override void Deserialize(ref MemoryPackReader reader, scoped ref IReadOnlyDictionary<TKey, TValue?>? value)
        {
            if (!reader.TryReadCollectionHeader(out var length))
            {
                value = null;
                return;
            }

            var dict = new Dictionary<TKey, TValue?>(equalityComparer);

            var keyFormatter = reader.GetFormatter<TKey>();
            var valueFormatter = reader.GetFormatter<TValue>();
            for (int i = 0; i < length; i++)
            {
                KeyValuePairFormatter.Deserialize(keyFormatter, valueFormatter, ref reader, out var k, out var v);
                dict.Add(k!, v);
            }

            value = dict;
        }
    }
}
