using System;
using System.Collections.Generic;
using Shared;

namespace Internal
{
    // What this client knows about an object id of the session.
    public enum NetworkObjectState
    {
        // Never seen: a message for it is a bug of the protocol (an exception).
        Unknown,

        // CreatedOverview received, the listener still builds the object: its updates and events wait for it.
        Creating,

        Live,

        // Was here and is gone (destroyed, its owner left, its creation failed). Ids are not reused in a session,
        // so a message for it is a race with the destroy and is dropped.
        Destroyed
    }

    public interface INetworkObjectsCollection
    {
        IReadOnlyDictionary<int, INetworkObject> Entries { get; }

        NetworkObjectState GetState(int id);

        // The object is live: its properties go to the sync, the updates and events buffered while it was being
        // created are applied (a property update only if newer than the value the object has). Ends with the
        // lifetime of the object - the id becomes destroyed.
        void Add(INetworkObject networkObject);

        // A remote entity is being built (from CreatedOverview to the end of its listener).
        void BeginCreation(int id);

        // The creation is cancelled (destroyed while being built) or failed: the id becomes destroyed, the buffer
        // is dropped. Nothing for an id already destroyed.
        void CancelCreation(int id);

        void ApplyUpdate(SharedSessionObject.PropertyUpdate update);
        void ApplyEvent(SharedSessionObject.Event networkEvent);
    }

    public class NetworkObjectsCollection : INetworkObjectsCollection
    {
        public NetworkObjectsCollection(INetworkDirtyProperties dirtyProperties, ILog<NetworkObjectsCollection> log)
        {
            _dirtyProperties = dirtyProperties;
            _log = log;
        }

        private readonly INetworkDirtyProperties _dirtyProperties;
        private readonly ILog _log;

        private readonly Dictionary<int, INetworkObject> _entries = new();
        private readonly Dictionary<int, CreationBuffer> _creating = new();
        private readonly HashSet<int> _destroyed = new();

        private int _droppedMessages;

        public IReadOnlyDictionary<int, INetworkObject> Entries => _entries;

        public NetworkObjectState GetState(int id)
        {
            if (_entries.ContainsKey(id) == true)
                return NetworkObjectState.Live;

            if (_creating.ContainsKey(id) == true)
                return NetworkObjectState.Creating;

            if (_destroyed.Contains(id) == true)
                return NetworkObjectState.Destroyed;

            return NetworkObjectState.Unknown;
        }

        public void Add(INetworkObject networkObject)
        {
            var id = networkObject.Id;
            var state = GetState(id);

            if (state is NetworkObjectState.Live or NetworkObjectState.Destroyed)
                throw new InvalidOperationException($"[Network] Object {id} is added while it is {state}");

            _creating.Remove(id, out var buffer);

            // Destroyed before it got here (a local entity destroyed while its CreateRequest was on its way).
            if (networkObject.Lifetime.IsTerminated == true)
            {
                _destroyed.Add(id);
                return;
            }

            _entries.Add(id, networkObject);

            networkObject.Lifetime.Listen(() => {
                _entries.Remove(id);
                _destroyed.Add(id);
            });

            foreach (var (_, property) in networkObject.Properties)
                property.Attach(networkObject, _dirtyProperties);

            if (buffer != null)
                Flush(networkObject, buffer);
        }

        public void BeginCreation(int id)
        {
            var state = GetState(id);

            if (state != NetworkObjectState.Unknown)
                throw new InvalidOperationException($"[Network] Creation of object {id} that is already {state}");

            _creating.Add(id, new CreationBuffer());
        }

        public void CancelCreation(int id)
        {
            if (_creating.Remove(id) == true)
            {
                _destroyed.Add(id);
                return;
            }

            var state = GetState(id);

            if (state != NetworkObjectState.Destroyed)
                throw new InvalidOperationException($"[Network] Cancelled creation of object {id} that is {state}");
        }

        public void ApplyUpdate(SharedSessionObject.PropertyUpdate update)
        {
            var id = update.ObjectId;

            if (_entries.TryGetValue(id, out var networkObject) == true)
            {
                Apply(networkObject, update);
                return;
            }

            if (_creating.TryGetValue(id, out var buffer) == true)
            {
                // Only the newest value of a property matters: it replaces the older ones.
                if (buffer.Updates.TryGetValue(update.PropertyId, out var buffered) == false ||
                    buffered.Version < update.Version)
                {
                    buffer.Updates[update.PropertyId] = update;
                }

                return;
            }

            Drop(id, update);
        }

        public void ApplyEvent(SharedSessionObject.Event networkEvent)
        {
            var id = networkEvent.ObjectId;

            if (_entries.TryGetValue(id, out var networkObject) == true)
            {
                networkObject.Events.Invoke(networkEvent.Value);
                return;
            }

            if (_creating.TryGetValue(id, out var buffer) == true)
            {
                buffer.Events.Add(networkEvent);
                return;
            }

            Drop(id, networkEvent);
        }

        // The buffered updates first (the state the events may act on), then the events in their order.
        private void Flush(INetworkObject networkObject, CreationBuffer buffer)
        {
            foreach (var (_, update) in buffer.Updates)
            {
                if (networkObject.Lifetime.IsTerminated == true)
                    return;

                Apply(networkObject, update);
            }

            foreach (var networkEvent in buffer.Events)
            {
                if (networkObject.Lifetime.IsTerminated == true)
                    return;

                networkObject.Events.Invoke(networkEvent.Value);
            }
        }

        private static void Apply(INetworkObject networkObject, SharedSessionObject.PropertyUpdate update)
        {
            if (networkObject.Properties.TryGetValue(update.PropertyId, out var property) == false)
            {
                throw new InvalidOperationException(
                    $"[Network] PropertyUpdate for property {update.PropertyId} that object {update.ObjectId} does not have");
            }

            // The backend versions every write; an older one (or the value the overview already had) changes nothing.
            if (property.Version >= update.Version)
                return;

            property.Update(update.Value, update.Version);
        }

        private void Drop(int id, INetworkContext context)
        {
            if (_destroyed.Contains(id) == false)
            {
                throw new InvalidOperationException(
                    $"[Network] {ErrorResponse.Describe(context)} for object {id} that this client has never had");
            }

            _droppedMessages++;

            _log.Debug("{Message} for destroyed object {Id} dropped ({Dropped} in the session)", ErrorResponse.Describe(context), id, _droppedMessages);
        }

        private class CreationBuffer
        {
            public readonly Dictionary<int, SharedSessionObject.PropertyUpdate> Updates = new();
            public readonly List<SharedSessionObject.Event> Events = new();
        }
    }
}
