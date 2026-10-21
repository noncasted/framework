using System;
using System.Collections.Generic;
using MemoryPack;
using Shared;
using UnityEngine;

namespace Internal
{
    // A network service of a local session: the counterpart of the backend Game.Session.Service. Property updates
    // go to the users that fetched the service (GetRequest); a write of the client goes to the other subscribers, a
    // write of the service itself to all of them.
    public class LocalSessionService
    {
        public LocalSessionService(string key)
        {
            Key = key;
            Id = GetId(key);
        }

        private readonly Dictionary<int, LocalServiceProperty> _properties = new();
        private readonly List<LocalSessionConnection> _subscribers = new();

        public string Key { get; }
        public int Id { get; }

        protected LocalSession Session { get; private set; }
        protected IReadOnlyLifetime Lifetime { get; private set; }

        // As the backend Service.GetId: stable across processes and negative, never the id of an entity.
        public static int GetId(string key)
        {
            return StableHash.Get(key) | int.MinValue;
        }

        public void BindProperty(LocalServiceProperty property)
        {
            if (_properties.ContainsKey(property.Id) == true)
                throw new InvalidOperationException($"[Network] [Local] Service {Key} already has property {property.Id}");

            property.Bind(this);
            _properties.Add(property.Id, property);
        }

        internal void Start(LocalSession session, IReadOnlyLifetime lifetime)
        {
            Session = session;
            Lifetime = lifetime;
            OnStarted(lifetime);
        }

        internal void AddSubscriber(LocalSessionConnection user)
        {
            if (_subscribers.Contains(user) == true)
                return;

            _subscribers.Add(user);
            user.Lifetime.Listen(() => _subscribers.Remove(user));
        }

        internal void Write(LocalSessionConnection writer, int propertyId, byte[] value)
        {
            if (_properties.TryGetValue(propertyId, out var property) == false)
                throw new InvalidOperationException($"[Network] [Local] Service {Key} has no property {propertyId}");

            // Not the writer of the property: dropped, as SetPropertyCommand of the backend does.
            if (property.CanBeWrittenBy(LocalSession.UserIndex) == false)
            {
                Debug.LogWarning($"[Network] [Local] User {LocalSession.UserIndex} is not the writer of property {propertyId} of {Key}: ignored");
                return;
            }

            property.Update(value);

            var update = CreateUpdate(property);

            foreach (var subscriber in _subscribers.ToArray())
            {
                if (subscriber != writer)
                    subscriber.Send(update);
            }
        }

        internal void Push(LocalServiceProperty property)
        {
            var update = CreateUpdate(property);

            foreach (var subscriber in _subscribers.ToArray())
                subscriber.Send(update);
        }

        public IReadOnlyList<SharedSessionObject.PropertyUpdate> CreateOverview()
        {
            var properties = new List<SharedSessionObject.PropertyUpdate>();

            foreach (var (_, property) in _properties)
                properties.Add(CreateUpdate(property));

            return properties;
        }

        protected virtual void OnStarted(IReadOnlyLifetime lifetime)
        {
        }

        // The user that owns the session now (the backend SessionOwner): the local session has one, from its join.
        protected internal virtual void OnOwnerChanged(int ownerIndex)
        {
        }

        private SharedSessionObject.PropertyUpdate CreateUpdate(LocalServiceProperty property)
        {
            return new SharedSessionObject.PropertyUpdate
            {
                ObjectId = Id,
                PropertyId = property.Id,
                Value = property.RawValue,
                Version = property.Version
            };
        }
    }

    // The counterpart of the backend ObjectProperty: raw bytes and a version raised by every write.
    public class LocalServiceProperty
    {
        public LocalServiceProperty(int id, byte[] rawValue)
        {
            Id = id;
            RawValue = rawValue;
        }

        private LocalSessionService _service;
        private Func<int, bool> _writerCheck;

        public int Id { get; }
        public int Version { get; private set; }
        public byte[] RawValue { get; private set; }

        internal void Bind(LocalSessionService service)
        {
            _service = service;
        }

        // A property has one writer; without a restriction the client writes it.
        public void RestrictWriter(Func<int, bool> check)
        {
            if (_writerCheck != null)
                throw new InvalidOperationException($"[Network] [Local] Property {Id} already has a writer restriction");

            _writerCheck = check;
        }

        public bool CanBeWrittenBy(int userIndex)
        {
            return _writerCheck == null || _writerCheck(userIndex) == true;
        }

        // A write of the client.
        public virtual void Update(byte[] value)
        {
            SetRaw(value);
        }

        // A write of the service itself: goes to the subscribers.
        public void Set(byte[] value)
        {
            SetRaw(value);
            Push();
        }

        protected void Push()
        {
            _service.Push(this);
        }

        protected void SetRaw(byte[] value)
        {
            RawValue = value;
            Version++;
        }
    }

    // The counterpart of the backend ValueProperty: the value is kept deserialized, the writes of the client are
    // reported by Written.
    public class LocalValueProperty<T> : LocalServiceProperty where T : new()
    {
        public LocalValueProperty(int id) : this(id, new T())
        {
        }

        public LocalValueProperty(int id, T value) : base(id, MemoryPackSerializer.Serialize(value))
        {
            Value = value;
        }

        private readonly ViewableDelegate<T> _written = new();

        public T Value { get; private set; }
        public IViewableDelegate<T> Written => _written;

        public void Set(T value)
        {
            Value = value;
            SetRaw(MemoryPackSerializer.Serialize(value));
            Push();
        }

        public void Update(Action<T> action)
        {
            action(Value);
            Set(Value);
        }

        public override void Update(byte[] value)
        {
            var deserialized = MemoryPackSerializer.Deserialize<T>(value);

            if (deserialized == null)
                throw new InvalidOperationException($"[Network] [Local] Property {Id} is written with a null {typeof(T).Name}");

            base.Update(value);
            Value = deserialized;
            _written.Invoke(deserialized);
        }
    }
}
