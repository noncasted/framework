using System;
using System.Collections.Generic;
using MemoryPack;
using Shared;

namespace Internal
{
    public interface INetworkEvents
    {
        IReadOnlyDictionary<Type, object> Entries { get; }

        void AddSource(Type type, object source, Action<IEventPayload> invoke);
        void Invoke(byte[] rawPayload);
        void Send(IEventPayload rawPayload);
    }

    // The events of one network object. An event of a type nobody has asked for yet (GetEvent) is kept, in the order of
    // arrival, and handed to the source of its type when it is created: the events of an object start to flow as soon
    // as the object is live, the listeners of a type may come later (a role taken after the entry into the session).
    public class NetworkEvents : INetworkEvents
    {
        public NetworkEvents(INetworkConnection connection, INetworkObject networkObject)
        {
            _connection = connection;
            _object = networkObject;
        }

        // A type nobody listens to for that long is a bug of the game, not a late listener.
        public const int MaxPendingEvents = 64;

        private readonly INetworkConnection _connection;
        private readonly INetworkObject _object;

        private readonly Dictionary<Type, object> _entries = new();
        private readonly Dictionary<Type, Action<IEventPayload>> _actions = new();

        // The events without a source, of every type in the order of arrival; created with the first of them.
        private List<IEventPayload> _pending;

        public IReadOnlyDictionary<Type, object> Entries => _entries;

        public void AddSource(Type type, object source, Action<IEventPayload> invoke)
        {
            _entries[type] = source;
            _actions[type] = invoke;

            if (_pending != null)
                MovePending(type, (INetworkEventSource)source);
        }

        public void Invoke(byte[] rawPayload)
        {
            var payload = MemoryPackSerializer.Deserialize<IEventPayload>(rawPayload);
            var type = payload.GetType();

            if (_actions.TryGetValue(type, out var action) == true)
            {
                action.Invoke(payload);
                return;
            }

            _pending ??= new List<IEventPayload>();

            if (_pending.Count == MaxPendingEvents)
            {
                throw new InvalidOperationException(
                    $"[Network] No source of event type {type}: {MaxPendingEvents} events of the object wait already");
            }

            _pending.Add(payload);
        }

        public void Send(IEventPayload rawPayload)
        {
            _connection.OneWay(new SharedSessionObject.Event()
            {
                ObjectId = _object.Id,
                Value = MemoryPackSerializer.Serialize(rawPayload)
            });
        }

        // The events of the type go to its source in their order, the others keep theirs.
        private void MovePending(Type type, INetworkEventSource source)
        {
            var kept = 0;

            for (var index = 0; index < _pending.Count; index++)
            {
                var payload = _pending[index];

                if (payload.GetType() == type)
                    source.Hold(payload);
                else
                    _pending[kept++] = payload;
            }

            _pending.RemoveRange(kept, _pending.Count - kept);
        }
    }

    public static class NetworkEventsExtensions
    {
        public static IViewableDelegate<T> GetEvent<T>(this INetworkEvents events) where T : IEventPayload
        {
            var type = typeof(T);

            if (events.Entries.ContainsKey(type) == false)
            {
                var source = new NetworkEventSource<T>();

                events.AddSource(type, source, payload => {
                    if (payload is not T castedPayload)
                        throw new InvalidCastException();

                    source.Receive(castedPayload);
                });
            }

            return events.Entries[type] as ViewableDelegate<T>;
        }

        // Passes the event to the listeners of this client only, nothing is sent: the sender of an event does not
        // get it back, so a sender that must act on its own event invokes it here.
        public static void InvokeLocal<T>(this INetworkEvents events, T payload) where T : IEventPayload
        {
            ((ViewableDelegate<T>)events.GetEvent<T>()).Invoke(payload);
        }
    }
}