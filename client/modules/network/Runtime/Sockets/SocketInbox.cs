using System;
using System.Collections.Concurrent;

namespace Internal
{
    // What a socket received, from any thread, handed over in the player loop in its order. A failed handler is reported
    // and the next items go on: a stopped loop would leave a socket that never reports anything again.
    public sealed class SocketInbox<T>
    {
        public SocketInbox(IReadOnlyLifetime lifetime, Action<T> handler)
        {
            _lifetime = lifetime;
            _handler = handler;
        }

        private static readonly ILog _log = Logs.Of<SocketInbox<T>>();

        private readonly IReadOnlyLifetime _lifetime;
        private readonly Action<T> _handler;
        private readonly ConcurrentQueue<T> _queue = new();

        public void Enqueue(T item)
        {
            _queue.Enqueue(item);
        }

        // An item a handler adds while the queue is drained goes in the same pass.
        public void Drain()
        {
            while (_lifetime.IsTerminated == false && _queue.TryDequeue(out var item) == true)
            {
                try
                {
                    _handler(item);
                }
                catch (Exception exception)
                {
                    _log.Exception(exception);
                }
            }
        }
    }
}
