using System;

namespace Global.Telemetry
{
    // A bounded queue of reusable records: when it is full, a new record takes the place of the oldest.
    public class TelemetryRing<T> where T : class, new()
    {
        public TelemetryRing(int capacity)
        {
            _items = new T[capacity];

            for (var i = 0; i < capacity; i++)
                _items[i] = new T();
        }

        private readonly T[] _items;
        private int _head;

        public int Count { get; private set; }

        // The next record to fill.
        public T Add()
        {
            var item = _items[(_head + Count) % _items.Length];

            if (Count == _items.Length)
                _head = (_head + 1) % _items.Length;
            else
                Count++;

            return item;
        }

        // From the oldest.
        public T At(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"[Telemetry] {Count} records in the ring");

            return _items[(_head + index) % _items.Length];
        }

        public void RemoveOldest(int count)
        {
            if (count < 0 || count > Count)
                throw new ArgumentOutOfRangeException(nameof(count), count, $"[Telemetry] {Count} records in the ring");

            _head = (_head + count) % _items.Length;
            Count -= count;
        }
    }
}
