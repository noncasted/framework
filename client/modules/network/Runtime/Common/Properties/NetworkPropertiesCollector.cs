using System.Collections.Generic;
using Shared;

namespace Internal
{
    // Sends the properties written on this client: a property reports its first write after a sync
    // (INetworkDirtyProperties), and every PropertiesSyncInterval all reported ones go out in one SetProperties.
    // A tick without writes allocates nothing and touches no object.
    public class NetworkPropertiesCollector : IUpdatable, IScopeSetup, INetworkDirtyProperties
    {
        public NetworkPropertiesCollector(INetworkConnection connection, IUpdater updater, NetworkOptions options)
        {
            _connection = connection;
            _updater = updater;
            _options = options;
        }

        private readonly INetworkConnection _connection;
        private readonly IUpdater _updater;
        private readonly NetworkOptions _options;

        private readonly List<DirtyEntry> _dirty = new();

        private float _timer = 0f;
        private double _lastTime;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _lastTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
            _updater.Add(lifetime, this);
        }

        public void OnDirty(INetworkObject owner, INetworkProperty property)
        {
            _dirty.Add(new DirtyEntry(owner, property));
        }

        // The delta of the updater is the game time (0 while the game is paused): the sync of the session goes on
        // in a pause, so it runs on real time. The updater still orders it after the writes of the frame.
        public void OnUpdate(float delta)
        {
            var interval = _options.PropertiesSyncInterval;

            var now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            _timer += (float)(now - _lastTime);
            _lastTime = now;

            if (_timer < interval)
                return;

            // The remainder carries over: with a reset to zero a 16.6 ms frame falls short of the interval
            // and the send goes out a frame later, effectively at 30 Hz. A long frame does not accumulate debt.
            _timer = UnityEngine.Mathf.Min(_timer - interval, interval);

            if (_dirty.Count == 0)
                return;

            // A new list per message: the writer serializes it later, in its own loop.
            var entries = new List<SharedSessionObject.SetProperty>(_dirty.Count);

            foreach (var (owner, property) in _dirty)
            {
                // The object is gone: nobody receives its writes (the backend would drop them too).
                if (owner.Lifetime.IsTerminated == true)
                    continue;

                entries.Add(new SharedSessionObject.SetProperty()
                {
                    ObjectId = owner.Id,
                    PropertyId = property.Id,
                    Value = property.Collect()
                });

                // The server versions this write once more and does not send it back to this client.
                property.OnWriteSent();
            }

            _dirty.Clear();

            if (entries.Count == 0)
                return;

            _connection.OneWay(new SharedSessionObject.SetProperties() { Entries = entries });
        }

        private readonly struct DirtyEntry
        {
            public DirtyEntry(INetworkObject owner, INetworkProperty property)
            {
                Owner = owner;
                Property = property;
            }

            public readonly INetworkObject Owner;
            public readonly INetworkProperty Property;

            public void Deconstruct(out INetworkObject owner, out INetworkProperty property)
            {
                owner = Owner;
                property = Property;
            }
        }
    }
}
