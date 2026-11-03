using System;

namespace Global.Telemetry
{
    // The bridge from any thread to the main one. No garbage: an array of references to the strings that exist anyway.
    // When full the new errors are lost, the first ones are usually the cause of the rest. One error in a row takes one slot.
    public class TelemetryErrorQueue
    {
        private readonly object _lock = new();
        private readonly TelemetryQueuedError[] _items = new TelemetryQueuedError[TelemetryErrorLimits.MaxQueued];
        private int _count;
        private int _warnings;
        private bool _isClosed;

        public int Count
        {
            get
            {
                lock (_lock)
                    return _count;
            }
        }

        public bool IsClosed
        {
            get
            {
                lock (_lock)
                    return _isClosed;
            }
        }

        // Any thread.
        public void Add(TelemetryErrorLevel level, string message, string stack)
        {
            lock (_lock)
            {
                if (_isClosed)
                    return;

                if (_count > 0 && _items[_count - 1].IsSame(level, message, stack))
                {
                    _items[_count - 1].CountOnce();
                    return;
                }

                if (_count == _items.Length)
                    return;

                if (level == TelemetryErrorLevel.Warning)
                {
                    if (_warnings == TelemetryErrorLimits.MaxQueuedWarnings)
                        return;

                    _warnings++;
                }

                _items[_count++] = new TelemetryQueuedError(level, message, stack);
            }
        }

        // Moves everything into an array of the same size, the order is kept.
        public int Drain(TelemetryQueuedError[] into)
        {
            lock (_lock)
            {
                var count = _count;

                Array.Copy(_items, into, count);
                Array.Clear(_items, 0, count);
                _count = 0;
                _warnings = 0;

                return count;
            }
        }

        // A new session.
        public void Open()
        {
            lock (_lock)
                _isClosed = false;
        }

        // The session is over or its limit is spent: nothing is taken and nothing is held any more.
        public void Close()
        {
            lock (_lock)
            {
                _isClosed = true;
                Array.Clear(_items, 0, _count);
                _count = 0;
                _warnings = 0;
            }
        }
    }
}
