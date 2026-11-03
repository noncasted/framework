using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // NetworkPropertiesCollector: the sync of the written properties runs on real time, not on the game time of the
    // updater; a local write takes the place of the server version it will get, so an older write of another client
    // that the server ordered before it does not replace it here.
    public class NetworkPropertiesCollectorTests
    {
        private const int ObjectId = 1048577;
        private const int PropertyId = 7;

        // The game is paused (the updater speed is 0, every delta is 0): the writes still go out.
        [UnityTest]
        public IEnumerator UpdaterPaused_WritesAreStillSent() => UniTask.ToCoroutine(async () =>
        {
            var world = new World();
            world.Updater.Pause();

            world.Object.Property.Set(5);

            for (var i = 0; i < 10; i++)
            {
                await UniTask.Delay(20, DelayType.Realtime);
                world.Updater.Tick();
            }

            Assert.AreEqual(1, world.Writer.OneWays.Count);
            world.Lifetime.Terminate();
        });

        // Clients A (this one) and B write the property at once. The server applies B first (version 1, sent to A) and
        // then A (version 2, sent to B only: the writer does not get its write back). The value of the server is A's:
        // this client keeps it instead of taking B's older write.
        [UnityTest]
        public IEnumerator ConcurrentWrites_RemoteOrderedBefore_LocalKeepsServerValue() => UniTask.ToCoroutine(async () =>
        {
            var world = new World();

            world.Object.Property.Set(5);
            await world.Sync();

            Assert.AreEqual(1, world.Writer.OneWays.Count);

            world.Objects.ApplyUpdate(new SharedSessionObject.PropertyUpdate
            {
                ObjectId = ObjectId,
                PropertyId = PropertyId,
                Value = Bytes(9),
                Version = 1
            });

            Assert.AreEqual(5, world.Object.Property.Value);

            // A later write of B (version 3, after A's on the server) is applied.
            world.Objects.ApplyUpdate(new SharedSessionObject.PropertyUpdate
            {
                ObjectId = ObjectId,
                PropertyId = PropertyId,
                Value = Bytes(11),
                Version = 3
            });

            Assert.AreEqual(11, world.Object.Property.Value);
            world.Lifetime.Terminate();
        });

        private static byte[] Bytes(int value)
        {
            var property = new NetworkProperty<int>(0);
            property.Set(value);
            return property.Collect();
        }

        private class World
        {
            public World()
            {
                var options = ScriptableObject.CreateInstance<NetworkOptions>();
                Collector = new NetworkPropertiesCollector(new FakeConnection(Writer), Updater, options);
                Collector.OnSetup(Lifetime);

                Objects = new NetworkObjectsCollection(Collector);
                Objects.Add(Object);
            }

            public readonly Lifetime Lifetime = new();
            public readonly FakeUpdater Updater = new();
            public readonly FakeWriter Writer = new();
            public readonly NetworkPropertiesCollector Collector;
            public readonly NetworkObjectsCollection Objects;
            public readonly TestObject Object = new(ObjectId);

            // Long enough for a sync interval of the default options to pass.
            public async UniTask Sync()
            {
                for (var i = 0; i < 5; i++)
                {
                    await UniTask.Delay(20, DelayType.Realtime);
                    Updater.Tick();
                }
            }
        }

        private class TestObject : INetworkObject
        {
            public TestObject(int id)
            {
                Id = id;
                Properties = new Dictionary<int, INetworkProperty> { { PropertyId, Property } };
            }

            public readonly NetworkProperty<int> Property = new(PropertyId);
            private readonly Lifetime _lifetime = new();

            public int Id { get; }
            public IReadOnlyDictionary<int, INetworkProperty> Properties { get; }
            public INetworkEvents Events => throw new NotSupportedException();
            public IReadOnlyLifetime Lifetime => _lifetime;
        }

        // The updater of the scope: the delta is the unscaled time by the speed, 0 while paused.
        private class FakeUpdater : IUpdater
        {
            private readonly ViewableProperty<float> _speed = new(1f);
            private readonly List<IUpdatable> _updatables = new();

            public IViewableProperty<float> Speed => _speed;

            public void Tick()
            {
                foreach (var updatable in _updatables)
                    updatable.OnUpdate(0.02f * _speed.Value);
            }

            public void SetSpeed(float speed) => _speed.Set(speed);
            public void Pause() => _speed.Set(0f);
            public void Continue() => _speed.Set(1f);

            public void Add(IReadOnlyLifetime l, IUpdatable u) => _updatables.Add(u);
            public void Add(IReadOnlyLifetime l, IPreUpdatable updatable) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IFixedUpdatable u) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IPostFixedUpdatable updatable) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IGizmosUpdatable updatable) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, IPostUpdatable updatable) => throw new NotSupportedException();
            public void Add(IReadOnlyLifetime l, ILateUpdatable updatable) => throw new NotSupportedException();
        }

        private class FakeWriter : IConnectionWriter
        {
            public readonly List<INetworkContext> OneWays = new();

            public bool IsClosed => false;
            public object CurrentRun => this;

            public void WriteOneWay(INetworkContext value) => OneWays.Add(value);
            public UniTask<T> WriteRequest<T>(INetworkContext commandContext) => throw new NotSupportedException();
            public void WriteResponse(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public void OnRequestHandled(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public UniTask ForceSendAll() => throw new NotSupportedException();
        }

        private class FakeConnection : INetworkConnection
        {
            public FakeConnection(IConnectionWriter writer)
            {
                Writer = writer;
            }

            public IConnectionReader Reader => throw new NotSupportedException();
            public IConnectionWriter Writer { get; }
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();
            public NetworkTraffic Traffic { get; } = new();

            public UniTask Run(IReadOnlyLifetime lifetime, string url) => throw new NotSupportedException();
            public void Drop(string reason) => throw new NotSupportedException();
        }
    }
}
