using MemoryPack.Formatters;
using MemoryPack.Internal;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Collection formatters of this build: List, Dictionary (the others of the original are removed, see FORK.md)

namespace MemoryPack
{
    public static partial class MemoryPackFormatterProvider
    {
        static readonly Dictionary<Type, Type> CollectionFormatters = new Dictionary<Type, Type>(2)
        {
            { typeof(List<>), typeof(ListFormatter<>) },
            { typeof(Dictionary<,>), typeof(DictionaryFormatter<,>) },
        };
    }
}

namespace MemoryPack.Formatters
{
    [Preserve]
    public static class ListFormatter
    {
        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializePackable<T, TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, List<T?>? value)
            where T : IMemoryPackable<T>
#if NET7_0_OR_GREATER
            where TBufferWriter : IBufferWriter<byte>
#else
            where TBufferWriter : class, IBufferWriter<byte>
#endif
        {
            if (value == null)
            {
                writer.WriteNullCollectionHeader();
                return;
            }

#if NET7_0_OR_GREATER
            writer.WritePackableSpan(CollectionsMarshal.AsSpan(value));
#else
            var formatter = writer.GetFormatter<T?>();
            writer.WriteCollectionHeader(value.Count);
            foreach (var item in value)
            {
                var v = item;
                formatter.Serialize(ref writer, ref v);
            }
#endif
        }

        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T?>? DeserializePackable<T>(ref MemoryPackReader reader)
            where T : IMemoryPackable<T>
        {
            List<T?>? value = default;
            DeserializePackable<T>(ref reader, ref value);
            return value;
        }

        [Preserve]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DeserializePackable<T>(ref MemoryPackReader reader, scoped ref List<T?>? value)
            where T : IMemoryPackable<T>
        {
            if (!reader.TryReadCollectionHeader(out var length))
            {
                value = null;
                return;
            }

            if (value == null)
            {
                value = new List<T?>(length);
            }
#if NET7_0_OR_GREATER
            else if (value.Count == length)
            {
                value.Clear();
            }

            var span = CollectionsMarshalEx.CreateSpan(value, length);
            reader.ReadPackableSpanWithoutReadLengthHeader(length, ref span);
#else
            else
            {
                value.Clear();
            }
            var formatter = reader.GetFormatter<T?>();
            for (var i = 0; i < length; i++)
            {
                T? v = default;
                formatter.Deserialize(ref reader, ref v);
                value.Add(v);
            }
#endif
        }
    }

    [Preserve]
    public sealed class ListFormatter<T> : MemoryPackFormatter<List<T?>>
    {
        [Preserve]
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref List<T?>? value)
        {
            if (value == null)
            {
                writer.WriteNullCollectionHeader();
                return;
            }
#if NET7_0_OR_GREATER
            writer.WriteSpan(CollectionsMarshal.AsSpan(value));
#else
            var formatter = writer.GetFormatter<T?>();
            writer.WriteCollectionHeader(value.Count);
            foreach (var item in value)
            {
                var v = item;
                formatter.Serialize(ref writer, ref v);
            }
#endif
        }

        [Preserve]
        public override void Deserialize(ref MemoryPackReader reader, scoped ref List<T?>? value)
        {
            if (!reader.TryReadCollectionHeader(out var length))
            {
                value = null;
                return;
            }

            if (value == null)
            {
                value = new List<T?>(length);
            }
#if NET7_0_OR_GREATER
            else if (value.Count == length)
            {
                value.Clear();
            }

            var span = CollectionsMarshalEx.CreateSpan(value, length);
            reader.ReadSpanWithoutReadLengthHeader(length, ref span);
#else
            else
            {
                value.Clear();
            }
            var formatter = reader.GetFormatter<T?>();
            for (var i = 0; i < length; i++)
            {
                T? v = default;
                formatter.Deserialize(ref reader, ref v);
                value.Add(v);
            }
#endif
        }
    }

    [Preserve]
    public sealed class DictionaryFormatter<TKey, TValue> : MemoryPackFormatter<Dictionary<TKey, TValue?>>
        where TKey : notnull
    {
        readonly IEqualityComparer<TKey>? equalityComparer;

        public DictionaryFormatter()
            : this(null)
        {

        }

        public DictionaryFormatter(IEqualityComparer<TKey>? equalityComparer)
        {
            this.equalityComparer = equalityComparer;
        }

        [Preserve]
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref Dictionary<TKey, TValue?>? value)
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
        public override void Deserialize(ref MemoryPackReader reader, scoped ref Dictionary<TKey, TValue?>? value)
        {
            if (!reader.TryReadCollectionHeader(out var length))
            {
                value = null;
                return;
            }

            if (value == null)
            {
                value = new Dictionary<TKey, TValue?>(length, equalityComparer);
            }
            else
            {
                value.Clear();
            }

            var keyFormatter = reader.GetFormatter<TKey>();
            var valueFormatter = reader.GetFormatter<TValue>();
            for (int i = 0; i < length; i++)
            {
                KeyValuePairFormatter.Deserialize(keyFormatter, valueFormatter, ref reader, out var k, out var v);
                value.Add(k!, v);
            }
        }
    }
}
