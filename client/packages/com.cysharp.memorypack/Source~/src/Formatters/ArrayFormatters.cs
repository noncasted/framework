using MemoryPack.Formatters;
using MemoryPack.Internal;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

// Array formatters of this build: T[], T[] where T: unmanaged (the array-like types of the original are removed, see FORK.md)

namespace MemoryPack.Formatters
{
    [Preserve]
    public sealed class UnmanagedArrayFormatter<T> : MemoryPackFormatter<T[]>
            where T : unmanaged
    {
        [Preserve]
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref T[]? value)
        {
            writer.WriteUnmanagedArray(value);
        }

        [Preserve]
        public override void Deserialize(ref MemoryPackReader reader, scoped ref T[]? value)
        {
            reader.ReadUnmanagedArray<T>(ref value);
        }
    }

    [Preserve]
    public sealed class DangerousUnmanagedArrayFormatter<T> : MemoryPackFormatter<T[]>
    {
        [Preserve]
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref T[]? value)
        {
            writer.DangerousWriteUnmanagedArray(value);
        }

        [Preserve]
        public override void Deserialize(ref MemoryPackReader reader, scoped ref T[]? value)
        {
            reader.DangerousReadUnmanagedArray<T>(ref value);
        }
    }

    [Preserve]
    public sealed class ArrayFormatter<T> : MemoryPackFormatter<T?[]>
    {
        [Preserve]
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref T?[]? value)
        {
            writer.WriteArray(value);
        }

        [Preserve]
        public override void Deserialize(ref MemoryPackReader reader, scoped ref T?[]? value)
        {
            reader.ReadArray(ref value);
        }
    }
}
