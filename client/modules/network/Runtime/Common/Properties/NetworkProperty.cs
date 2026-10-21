using System;
using MemoryPack;

namespace Internal
{
    public interface INetworkProperty
    {
        bool IsDirty { get; }
        int Id { get; }
        int Version { get; }

        void Update(byte[] value, int version);
        byte[] Collect();

        // The object of the property is live in the session (INetworkObjectsCollection.Add): from now on every
        // write is reported to the sink (NetworkPropertiesCollector), a write made before - right here.
        void Attach(INetworkObject owner, INetworkDirtyProperties sink);
    }

    public interface INetworkProperty<T> : INetworkProperty, IViewableProperty<T> where T : new()
    {
        // Publishes the value: marks it for sending and notifies the local listeners. Always a change, also for
        // the current reference (a value mutated in place) or an equal value: the other clients receive every
        // sent value as a new update and notify their listeners, so the writer does the same.
        void Set(T value);
    }

    // The properties written since the last sync, in the order of their first write.
    public interface INetworkDirtyProperties
    {
        void OnDirty(INetworkObject owner, INetworkProperty property);
    }

    public class NetworkProperty<T> : INetworkProperty<T> where T : new()
    {
        public NetworkProperty(int id)
        {
            Id = id;
        }

        private readonly LifetimedValue<T> _lifetimedValue = new(new T());

        private bool _isDirty;
        private int _version;
        private INetworkObject _owner;
        private INetworkDirtyProperties _sink;

        public bool IsDirty => _isDirty;
        public int Id { get; }
        public int Version => _version;
        public T Value => _lifetimedValue.Value;
        public IReadOnlyLifetime ValueLifetime => _lifetimedValue.ValueLifetime;

        public void Set(T value)
        {
            if (Equals(_lifetimedValue.Value, value) == true)
                _lifetimedValue.Notify();
            else
                _lifetimedValue.Set(value);

            MarkDirty();
        }

        public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, T> handler)
        {
            _lifetimedValue.Advise(lifetime, handler);
        }

        public void Dispose()
        {
            _lifetimedValue.Dispose();
        }

        public void Update(byte[] value, int version)
        {
            var deserialized = MemoryPackSerializer.Deserialize<T>(value);
            _lifetimedValue.Set(deserialized);
            _version = version;
        }

        public byte[] Collect()
        {
            _isDirty = false;
            var serialized = MemoryPackSerializer.Serialize(Value);
            return serialized;
        }

        public void Attach(INetworkObject owner, INetworkDirtyProperties sink)
        {
            if (_sink != null)
            {
                throw new InvalidOperationException(
                    $"Network property {Id} ({typeof(T).Name}) of object {owner.Id} is already attached to object {_owner.Id}");
            }

            _owner = owner;
            _sink = sink;

            if (_isDirty == true)
                sink.OnDirty(owner, this);
        }

        private void MarkDirty()
        {
            if (_isDirty == true)
                return;

            _isDirty = true;
            _sink?.OnDirty(_owner, this);
        }
    }
}
