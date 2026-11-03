using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MemoryPack;
using NUnit.Framework;
using Shared;
using UnityEditor;
using UnityEngine;

namespace Internal.Tests
{
    // The network on the paths of every frame allocates nothing once its buffers have grown: the sync of the written
    // properties (collect, the SetProperties message, the send queue, the socket) and a one-way message with a reused
    // envelope. On WebGL (Boehm, not incremental) every collection stops the whole game.
    public class NetworkFrameAllocationTests
    {
        private const int Passes = 200;

        // Below a byte a pass: nothing is allocated per pass (a one-time allocation of the runtime aside, 48 B seen).
        // Asserts stay out of the passes, NUnit allocates in them.
        private const string PerPass = "bytes allocated by the passes";

        private int _completed;

        [SetUp]
        public void SetUp()
        {
            _completed = 0;
        }

        private void CountCompleted(UniTask send)
        {
            if (send.Status == UniTaskStatus.Succeeded)
                _completed++;
        }

        [Test]
        public void PropertiesSync_CollectWriteSend_AllocatesNothing()
        {
            var lifetime = new Lifetime();
            var socket = new CountingSocket();
            var run = new ConnectionWriterRun(lifetime, socket, new NetworkTraffic(), new PendingRequests());
            var options = ScriptableObject.CreateInstance<NetworkOptions>();
            var serialized = new SerializedObject(options);
            serialized.FindProperty("_propertiesSyncRate").floatValue = 1e9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var updater = new ManualUpdater();
            var collector = new NetworkPropertiesCollector(new RunConnection(run), updater, options);
            collector.OnSetup(lifetime);

            var objects = new NetworkObjectsCollection(collector);
            var target = new TestObject(1048577);
            objects.Add(target);

            // A snapshot mutated in place and published again, as a game does with its state.
            var snapshot = new List<int>();

            for (var i = 0; i < 1000; i++)
                snapshot.Add(i);

            void Pass()
            {
                snapshot[0]++;
                target.Snapshot.Set(snapshot);
                target.Position.Set(snapshot[0]);
                WaitRealTime();
                updater.Tick();
                CountCompleted(run.SendQueued());
            }

            for (var i = 0; i < 10; i++)
                Pass();

            var sends = socket.Sends;
            var bytes = Measure(Pass);

            Assert.AreEqual(Passes, socket.Sends - sends, "every pass sends its SetProperties");
            Assert.Less(bytes, Passes, PerPass);
            Assert.AreEqual(10 + Passes, _completed, "every send completes synchronously");

            UnityEngine.Object.DestroyImmediate(options);
            lifetime.Terminate();
        }

        [Test]
        public void OneWayMessage_EnqueueSend_AllocatesNothing()
        {
            var lifetime = new Lifetime();
            var socket = new CountingSocket();
            var run = new ConnectionWriterRun(lifetime, socket, new NetworkTraffic(), new PendingRequests());
            var ping = new SharedSessionLatency.Ping();
            var message = new OneWayMessageFromClient { Context = ping };

            void Pass()
            {
                ping.Id++;
                run.Enqueue(message);
                run.Enqueue(message);
                CountCompleted(run.SendQueued());
            }

            for (var i = 0; i < 10; i++)
                Pass();

            var sends = socket.Sends;

            Assert.Less(Measure(Pass), Passes, PerPass);
            Assert.AreEqual(10 + Passes, _completed, "every send completes synchronously");
            Assert.AreEqual(2 * Passes, socket.Sends - sends);
            CollectionAssert.AreEqual(MemoryPackSerializer.Serialize<IMessageFromClient>(message), socket.Last);
            lifetime.Terminate();
        }

        private static long Measure(Action pass)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < Passes; i++)
                pass.Invoke();

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        // The sync runs on real time: the next pass waits until the clock moves past the (tiny) interval.
        private static void WaitRealTime()
        {
            var start = Time.realtimeSinceStartupAsDouble;

            while (Time.realtimeSinceStartupAsDouble <= start)
            {
            }
        }

        // The updater of the scope: the delta is the unscaled time by the speed, 0 while paused.
        private class ManualUpdater : IUpdater
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

        private class TestObject : INetworkObject
        {
            public TestObject(int id)
            {
                Id = id;
                Properties = new Dictionary<int, INetworkProperty> { { 0, Snapshot }, { 1, Position } };
            }

            public readonly NetworkProperty<List<int>> Snapshot = new(0);
            public readonly NetworkProperty<int> Position = new(1);
            private readonly Lifetime _lifetime = new();

            public int Id { get; }
            public IReadOnlyDictionary<int, INetworkProperty> Properties { get; }
            public INetworkEvents Events => throw new NotSupportedException();
            public IReadOnlyLifetime Lifetime => _lifetime;
        }

        // Sends synchronously and keeps only the last message (copied outside the measured passes is not needed:
        // the copy is made when asked).
        private class CountingSocket : IWebSocket
        {
            private ArraySegment<byte> _last;

            public int Sends { get; private set; }
            public byte[] Last => _last.ToArray();

            public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();

            public UniTask Connect() => UniTask.CompletedTask;

            public UniTask Send(ArraySegment<byte> bytes)
            {
                Sends++;
                _last = bytes;
                return UniTask.CompletedTask;
            }

            public void Drop(string reason) => throw new NotSupportedException();
        }

        private class RunConnection : INetworkConnection, IConnectionWriter
        {
            public RunConnection(ConnectionWriterRun run)
            {
                _run = run;
            }

            private readonly ConnectionWriterRun _run;

            public IConnectionReader Reader => throw new NotSupportedException();
            public IConnectionWriter Writer => this;
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();
            public NetworkTraffic Traffic { get; } = new();
            public bool IsClosed => false;
            public object CurrentRun => _run;

            public UniTask Run(IReadOnlyLifetime lifetime, string url) => throw new NotSupportedException();
            public void Drop(string reason) => throw new NotSupportedException();

            public void WriteOneWay(ReadOnlySpan<byte> message) => _run.Enqueue(message);
            public void WriteOneWay(INetworkContext value) => throw new NotSupportedException();
            public UniTask<T> WriteRequest<T>(INetworkContext commandContext) => throw new NotSupportedException();
            public void WriteResponse(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public void OnRequestHandled(INetworkContext commandContext, int requestId) => throw new NotSupportedException();
            public UniTask ForceSendAll() => throw new NotSupportedException();
        }
    }
}
