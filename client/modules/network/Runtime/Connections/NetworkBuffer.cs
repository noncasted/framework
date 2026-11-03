using System;
using System.Buffers;

namespace Internal
{
    // A buffer the network serializes into again and again: it grows to the largest content and stays, Clear keeps
    // the array, so a steady stream of messages allocates nothing.
    public sealed class NetworkBuffer : IBufferWriter<byte>
    {
        public NetworkBuffer(int capacity = 1024)
        {
            _array = new byte[capacity];
        }

        private byte[] _array;
        private int _count;

        public int Count => _count;
        public ReadOnlySpan<byte> Written => new(_array, 0, _count);

        public ArraySegment<byte> Segment(int start, int length)
        {
            if (start < 0 || length < 0 || start + length > _count)
                throw new ArgumentOutOfRangeException(nameof(length), $"[Network] {start}+{length} is outside {_count} written bytes");

            return new ArraySegment<byte>(_array, start, length);
        }

        public ReadOnlySpan<byte> Span(int start, int length)
        {
            return Segment(start, length).AsSpan();
        }

        public void Clear()
        {
            _count = 0;
        }

        // Drops what was written after the count: a serialization that threw halfway.
        public void Truncate(int count)
        {
            if (count < 0 || count > _count)
                throw new ArgumentOutOfRangeException(nameof(count), $"[Network] {count} is outside {_count} written bytes");

            _count = count;
        }

        public void Advance(int count)
        {
            if (count < 0 || _count + count > _array.Length)
                throw new ArgumentOutOfRangeException(nameof(count), $"[Network] Advance by {count} past the buffer of {_array.Length}");

            _count += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _array.AsMemory(_count);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return _array.AsSpan(_count);
        }

        private void Ensure(int sizeHint)
        {
            var needed = _count + Math.Max(sizeHint, 1);

            if (needed <= _array.Length)
                return;

            var grown = new byte[Math.Max(needed, _array.Length * 2)];
            Buffer.BlockCopy(_array, 0, grown, 0, _count);
            _array = grown;
        }
    }
}
