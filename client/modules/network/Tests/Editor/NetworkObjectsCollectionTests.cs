using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;

namespace Internal.Tests
{
    // an object id is live, being created, destroyed or unknown. Updates and events of an object being created
    // wait for it (the newest update per property, events in order), of a destroyed one are dropped, of an unknown one
    // throw.
    public class NetworkObjectsCollectionTests
    {
        private const int ObjectId = 1048577;
        private const int PropertyId = 7;

        [Test]
        public void Update_LiveObject_AppliesNewerVersionOnly()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());
            var target = new TestObject(ObjectId);
            collection.Add(target);

            collection.ApplyUpdate(Update(5, version: 2));
            collection.ApplyUpdate(Update(4, version: 1));
            collection.ApplyUpdate(Update(6, version: 2));

            Assert.AreEqual(5, target.Property.Value);
            Assert.AreEqual(2, target.Property.Version);
        }

        [Test]
        public void Update_Creating_IsAppliedOnAdd_NewestPerProperty()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());
            collection.BeginCreation(ObjectId);

            collection.ApplyUpdate(Update(3, version: 3));
            collection.ApplyUpdate(Update(1, version: 1));
            collection.ApplyUpdate(Update(2, version: 2));

            Assert.AreEqual(NetworkObjectState.Creating, collection.GetState(ObjectId));

            var target = new TestObject(ObjectId);
            collection.Add(target);

            Assert.AreEqual(3, target.Property.Value);
            Assert.AreEqual(3, target.Property.Version);
            Assert.AreEqual(1, target.UpdateCount);
            Assert.AreEqual(NetworkObjectState.Live, collection.GetState(ObjectId));
        }

        [Test]
        public void Update_Creating_OlderThanOverview_IsNotApplied()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());
            collection.BeginCreation(ObjectId);
            collection.ApplyUpdate(Update(1, version: 4));

            // The listener filled the property from CreatedOverview with a newer version.
            var target = new TestObject(ObjectId);
            target.Property.Update(Bytes(9), 5);
            collection.Add(target);

            Assert.AreEqual(9, target.Property.Value);
            Assert.AreEqual(5, target.Property.Version);
        }

        [Test]
        public void Events_Creating_AreInvokedInOrderAfterUpdates()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());
            collection.BeginCreation(ObjectId);

            collection.ApplyEvent(Event(1));
            collection.ApplyUpdate(Update(5, version: 1));
            collection.ApplyEvent(Event(2));

            var target = new TestObject(ObjectId);
            var valueAtFirstEvent = -1;
            target.TestEvents.OnInvoke = _ => valueAtFirstEvent = valueAtFirstEvent < 0 ? target.Property.Value : valueAtFirstEvent;
            collection.Add(target);

            CollectionAssert.AreEqual(new[] { 1, 2 }, target.TestEvents.Received);
            Assert.AreEqual(5, valueAtFirstEvent);
        }

        [Test]
        public void CancelCreation_DropsBuffer_AndLaterMessagesAreDropped()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());
            collection.BeginCreation(ObjectId);
            collection.ApplyUpdate(Update(5, version: 1));

            collection.CancelCreation(ObjectId);
            collection.CancelCreation(ObjectId);

            Assert.AreEqual(NetworkObjectState.Destroyed, collection.GetState(ObjectId));
            Assert.DoesNotThrow(() => collection.ApplyUpdate(Update(6, version: 2)));
            Assert.DoesNotThrow(() => collection.ApplyEvent(Event(1)));
        }

        [Test]
        public void DestroyedObject_MessagesAreDropped()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());
            var target = new TestObject(ObjectId);
            collection.Add(target);

            target.LifetimeSource.Terminate();

            Assert.AreEqual(NetworkObjectState.Destroyed, collection.GetState(ObjectId));
            Assert.IsFalse(collection.Entries.ContainsKey(ObjectId));
            Assert.DoesNotThrow(() => collection.ApplyUpdate(Update(6, version: 2)));
            Assert.DoesNotThrow(() => collection.ApplyEvent(Event(1)));
            Assert.AreEqual(0, target.TestEvents.Received.Count);
        }

        [Test]
        public void UnknownObject_Throws()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());

            var update = Assert.Throws<InvalidOperationException>(() => collection.ApplyUpdate(Update(1, version: 1)));
            var networkEvent = Assert.Throws<InvalidOperationException>(() => collection.ApplyEvent(Event(1)));

            StringAssert.Contains(ObjectId.ToString(), update.Message);
            StringAssert.Contains("PropertyUpdate", update.Message);
            StringAssert.Contains("Event", networkEvent.Message);
        }

        [Test]
        public void UnknownProperty_OfLiveObject_Throws()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());
            collection.Add(new TestObject(ObjectId));

            var unknown = new SharedSessionObject.PropertyUpdate
            {
                ObjectId = ObjectId,
                PropertyId = PropertyId + 1,
                Value = Bytes(1),
                Version = 1
            };

            Assert.Throws<InvalidOperationException>(() => collection.ApplyUpdate(unknown));
        }

        [Test]
        public void BeginCreation_OfKnownId_Throws()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());
            var target = new TestObject(ObjectId);
            collection.Add(target);

            Assert.Throws<InvalidOperationException>(() => collection.BeginCreation(ObjectId));

            target.LifetimeSource.Terminate();

            Assert.Throws<InvalidOperationException>(() => collection.BeginCreation(ObjectId));
            Assert.Throws<InvalidOperationException>(() => collection.Add(new TestObject(ObjectId)));
        }

        [Test]
        public void Add_TerminatedObject_OnlyMarksDestroyed()
        {
            var collection = new NetworkObjectsCollection(new DirtySink());
            var target = new TestObject(ObjectId);
            target.LifetimeSource.Terminate();

            collection.Add(target);

            Assert.AreEqual(NetworkObjectState.Destroyed, collection.GetState(ObjectId));
        }

        [Test]
        public void Add_AttachesProperties_WriteBeforeIsReported()
        {
            var sink = new DirtySink();
            var collection = new NetworkObjectsCollection(sink);
            var target = new TestObject(ObjectId);
            target.Property.Set(1);

            collection.Add(target);
            target.Property.Set(2);

            Assert.AreEqual(1, sink.Entries.Count, "one report until the property is collected");
            Assert.AreSame(target, sink.Entries[0].Owner);

            target.Property.Collect();
            target.Property.Set(3);

            Assert.AreEqual(2, sink.Entries.Count);
        }

        [Test]
        public void Set_SameReference_NotifiesAndMarksDirty()
        {
            var property = new NetworkProperty<List<int>>(1);
            var value = property.Value;
            var notified = 0;

            property.Advise(new Lifetime(), (_, _) => notified++);
            property.Collect();
            value.Add(1);
            property.Set(value);

            Assert.AreEqual(1, notified);
            Assert.IsTrue(property.IsDirty);
        }

        [Test]
        public void Factory_DestroyWhileCreating_CancelsListener_AndDropsLaterMessages()
        {
            var world = new FactoryWorld();
            var listener = new UniTaskCompletionSource<INetworkEntity>();
            IReadOnlyLifetime creation = null;

            world.Factory.ListenRemote<TestPayload>(world.Lifetime, (lifetime, _) => {
                creation = lifetime;
                return listener.Task;
            });

            world.Factory.CreateRemote(world.Lifetime, world.Data(ObjectId)).Forget();

            Assert.AreEqual(NetworkObjectState.Creating, world.Objects.GetState(ObjectId));
            world.Objects.ApplyUpdate(Update(5, version: 1));

            Assert.IsTrue(world.Factory.TryCancelRemote(ObjectId));

            Assert.IsTrue(creation.IsTerminated);
            Assert.AreEqual(NetworkObjectState.Destroyed, world.Objects.GetState(ObjectId));
            Assert.DoesNotThrow(() => world.Objects.ApplyUpdate(Update(6, version: 2)));

            // The listener finishes anyway: its entity is destroyed, not added.
            var entity = world.CreateEntity(ObjectId);
            listener.TrySetResult(entity);

            Assert.IsTrue(entity.Lifetime.IsTerminated);
            Assert.IsFalse(world.Entities.Entries.ContainsKey(ObjectId));
            Assert.AreEqual(NetworkObjectState.Destroyed, world.Objects.GetState(ObjectId));
        }

        [Test]
        public void Factory_Created_AppliesBufferedUpdate()
        {
            var world = new FactoryWorld();
            var listener = new UniTaskCompletionSource<INetworkEntity>();

            world.Factory.ListenRemote<TestPayload>(world.Lifetime, (_, _) => listener.Task);
            world.Factory.CreateRemote(world.Lifetime, world.Data(ObjectId)).Forget();

            world.Objects.ApplyUpdate(Update(5, version: 1));

            var entity = world.CreateEntity(ObjectId);
            listener.TrySetResult(entity);

            Assert.AreEqual(NetworkObjectState.Live, world.Objects.GetState(ObjectId));
            Assert.AreSame(entity, world.Entities.Entries[ObjectId]);
            Assert.AreEqual(5, ((NetworkProperty<int>)entity.Properties[PropertyId]).Value);
        }

        [Test]
        public void Factory_ListenerFails_IdIsDestroyed()
        {
            var world = new FactoryWorld();
            var listener = new UniTaskCompletionSource<INetworkEntity>();

            world.Factory.ListenRemote<TestPayload>(world.Lifetime, (_, _) => listener.Task);
            var creation = world.Factory.CreateRemote(world.Lifetime, world.Data(ObjectId));

            listener.TrySetException(new InvalidOperationException("listener failed"));

            Assert.Throws<InvalidOperationException>(() => creation.GetAwaiter().GetResult());
            Assert.AreEqual(NetworkObjectState.Destroyed, world.Objects.GetState(ObjectId));
            Assert.DoesNotThrow(() => world.Objects.ApplyEvent(Event(1)));
        }

        // A listener that returns an entity of another id: the creation fails, the id does not stay Creating forever.
        [Test]
        public void Factory_ListenerReturnsOtherId_Throws_IdIsDestroyed()
        {
            var world = new FactoryWorld();
            var listener = new UniTaskCompletionSource<INetworkEntity>();

            world.Factory.ListenRemote<TestPayload>(world.Lifetime, (_, _) => listener.Task);
            var creation = world.Factory.CreateRemote(world.Lifetime, world.Data(ObjectId));

            var entity = world.CreateEntity(ObjectId + 1);
            listener.TrySetResult(entity);

            Assert.Throws<InvalidOperationException>(() => creation.GetAwaiter().GetResult());
            Assert.AreEqual(NetworkObjectState.Destroyed, world.Objects.GetState(ObjectId));
            Assert.AreEqual(NetworkObjectState.Unknown, world.Objects.GetState(ObjectId + 1));
            Assert.IsFalse(world.Entities.Entries.ContainsKey(ObjectId + 1));
        }

        [Test]
        public void Factory_Return_LocalEntity_GoesToRestoreListener_CountsTheSeat()
        {
            var world = new FactoryWorld();
            var listener = new UniTaskCompletionSource<INetworkEntity>();
            var restored = 0;
            var remote = 0;

            world.Factory.ListenRemote<TestPayload>(world.Lifetime, (_, _) => {
                remote++;
                return listener.Task;
            });

            world.Factory.ListenRestored<TestPayload>(world.Lifetime, (_, _) => {
                restored++;
                return listener.Task;
            });

            world.Factory.ExpectRestored(true, 1);
            var received = world.Factory.WaitRestoredReceived(world.Lifetime);
            Assert.AreEqual(UniTaskStatus.Pending, received.Status);

            var id = SharedEntityIds.Create(world.LocalIndex, 1);
            world.Factory.CreateRemote(world.Lifetime, world.LocalData(id)).Forget();

            Assert.AreEqual(1, restored);
            Assert.AreEqual(0, remote);
            Assert.AreEqual(UniTaskStatus.Succeeded, world.Factory.WaitRestoredReceived(world.Lifetime).Status);
            Assert.IsTrue(world.Factory.IsReturn);

            var entity = world.CreateEntity(id);
            listener.TrySetResult(entity);

            Assert.AreEqual(NetworkObjectState.Live, world.Objects.GetState(id));
        }

        [Test]
        public void Factory_LocalEntityWithoutReturn_Throws()
        {
            var world = new FactoryWorld();
            world.Factory.ListenRestored<TestPayload>(world.Lifetime, (_, _) => new UniTaskCompletionSource<INetworkEntity>().Task);
            world.Factory.ExpectRestored(false, 0);

            var creation = world.Factory.CreateRemote(world.Lifetime, world.LocalData(SharedEntityIds.Create(world.LocalIndex, 1)));

            Assert.Throws<InvalidOperationException>(() => creation.GetAwaiter().GetResult());
        }

        // An entity the backend has built for the local user (a player of a match): its id is issued by the backend,
        // it is created by the remote listener as any entity of the session, not restored.
        [Test]
        public void Factory_LocalEntityOfBackend_GoesToRemoteListener()
        {
            var world = new FactoryWorld();
            var listener = new UniTaskCompletionSource<INetworkEntity>();
            var restored = 0;
            var remote = 0;

            world.Factory.ListenRemote<TestPayload>(world.Lifetime, (_, _) => {
                remote++;
                return listener.Task;
            });

            world.Factory.ListenRestored<TestPayload>(world.Lifetime, (_, _) => {
                restored++;
                return listener.Task;
            });

            world.Factory.ExpectRestored(false, 0);

            var id = SharedEntityIds.CreateBackend(1);
            world.Factory.CreateRemote(world.Lifetime, world.LocalData(id)).Forget();

            Assert.AreEqual(0, restored);
            Assert.AreEqual(1, remote);

            listener.TrySetResult(world.CreateEntity(id));

            Assert.AreEqual(NetworkObjectState.Live, world.Objects.GetState(id));
        }

        [Test]
        public void Factory_NoReturn_RestoreWaitCompletesAtOnce_SecondExpectThrows()
        {
            var world = new FactoryWorld();
            world.Factory.ExpectRestored(false, 0);

            Assert.AreEqual(UniTaskStatus.Succeeded, world.Factory.WaitRestoredReceived(world.Lifetime).Status);
            Assert.Throws<InvalidOperationException>(() => world.Factory.ExpectRestored(true, 1));
        }

        [Test]
        public void User_Away_OnlyRemote()
        {
            var lifetime = new Lifetime();
            var remote = new NetworkUser(5, Guid.NewGuid(), false, lifetime.Child());
            var local = new NetworkUser(6, Guid.NewGuid(), true, lifetime.Child());

            remote.SetAway(true);
            Assert.IsTrue(remote.IsAway.Value);
            remote.SetAway(false);
            Assert.IsFalse(remote.IsAway.Value);

            Assert.Throws<InvalidOperationException>(() => local.SetAway(true));
        }

        private static SharedSessionObject.PropertyUpdate Update(int value, int version)
        {
            return new SharedSessionObject.PropertyUpdate
            {
                ObjectId = ObjectId,
                PropertyId = PropertyId,
                Value = Bytes(value),
                Version = version
            };
        }

        private static SharedSessionObject.Event Event(int value)
        {
            return new SharedSessionObject.Event
            {
                ObjectId = ObjectId,
                Value = Bytes(value)
            };
        }

        private static byte[] Bytes(int value)
        {
            var property = new NetworkProperty<int>(0);
            property.Set(value);
            return property.Collect();
        }

        private class DirtySink : INetworkDirtyProperties
        {
            public readonly List<(INetworkObject Owner, INetworkProperty Property)> Entries = new();

            public void OnDirty(INetworkObject owner, INetworkProperty property)
            {
                Entries.Add((owner, property));
            }
        }

        private class CountingProperty : NetworkProperty<int>, INetworkProperty
        {
            public CountingProperty() : base(PropertyId)
            {
            }

            public int UpdateCount { get; private set; }

            void INetworkProperty.Update(byte[] value, int version)
            {
                UpdateCount++;
                Update(value, version);
            }
        }

        // Events carry an int (MemoryPack bytes of NetworkProperty<int>) so the order is visible.
        private class TestEvents : INetworkEvents
        {
            public readonly List<int> Received = new();
            public Action<int> OnInvoke;

            public IReadOnlyDictionary<Type, object> Entries { get; } = new Dictionary<Type, object>();

            public void AddSource(Type type, object source, Action<IEventPayload> invoke)
            {
                throw new NotSupportedException();
            }

            public void Invoke(byte[] rawPayload)
            {
                var decoder = new NetworkProperty<int>(0);
                decoder.Update(rawPayload, 1);
                OnInvoke?.Invoke(decoder.Value);
                Received.Add(decoder.Value);
            }

            public void Send(IEventPayload rawPayload)
            {
                throw new NotSupportedException();
            }
        }

        private class TestObject : INetworkObject
        {
            public TestObject(int id)
            {
                Id = id;
                Properties = new Dictionary<int, INetworkProperty> { { PropertyId, _property } };
            }

            private readonly CountingProperty _property = new();

            public readonly Lifetime LifetimeSource = new();
            public readonly TestEvents TestEvents = new();

            public int Id { get; }
            public IReadOnlyDictionary<int, INetworkProperty> Properties { get; }
            public INetworkEvents Events => TestEvents;
            public IReadOnlyLifetime Lifetime => LifetimeSource;
            public NetworkProperty<int> Property => _property;
            public int UpdateCount => _property.UpdateCount;
        }

        private class TestPayload : IEntityPayload
        {
        }

        private class FactoryWorld
        {
            public FactoryWorld()
            {
                Objects = new NetworkObjectsCollection(new DirtySink());

                var session = new NetworkUser(NetworkUsersCollection.SessionIndex, Guid.Empty, false, Lifetime.Child());
                _users.Add(new NetworkUser(LocalIndex, Guid.NewGuid(), true, Lifetime.Child()));
                _users.SetSession(session);

                Factory = new NetworkEntityFactory(null, Entities, Objects, _users, new FixedIds(LocalIndex));
            }

            private readonly NetworkUsersCollection _users = new();

            public readonly int LocalIndex = 3;
            public readonly Lifetime Lifetime = new();
            public readonly NetworkEntityCollection Entities = new();
            public readonly NetworkObjectsCollection Objects;
            public readonly NetworkEntityFactory Factory;

            // An entity of the local user received from the session: the user has returned to its seat.
            public RemoteEntityData LocalData(int id)
            {
                return new RemoteEntityData(_users.Local, id, new List<SharedSessionObject.PropertyUpdate>(), new TestPayload());
            }

            public INetworkUsersCollection Users => _users;

            public RemoteEntityData Data(int id)
            {
                return new RemoteEntityData(_users.Session, id, new List<SharedSessionObject.PropertyUpdate>(), new TestPayload());
            }

            public INetworkEntity CreateEntity(int id)
            {
                return new NetworkEntity(null, null, _users.Session, id, new INetworkProperty[] { new NetworkProperty<int>(PropertyId) });
            }
        }

        private class FixedIds : INetworkEntityIds
        {
            public FixedIds(int userIndex)
            {
                _userIndex = userIndex;
            }

            private readonly int _userIndex;
            private int _counter;

            public int GetEntityId()
            {
                _counter++;
                return SharedEntityIds.Create(_userIndex, _counter);
            }

            public void Continue(int counter)
            {
                _counter = counter;
            }
        }
    }
}
