using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Internal.Tests
{
    public class UpdaterProxyTests
    {
        [Test]
        public void ParentSpeed_IsAppliedOnce()
        {
            var lifetime = new Lifetime();
            var (parent, proxy) = Create(lifetime);
            var target = new DeltaRecorder();

            parent.SetSpeed(2f);
            proxy.Add(lifetime, target);
            parent.Tick(0.1f);

            Assert.AreEqual(0.2f, target.Deltas[0], 0.0001f);

            lifetime.Terminate();
        }

        [Test]
        public void OwnSpeed_ScalesTheDelta()
        {
            var lifetime = new Lifetime();
            var (parent, proxy) = Create(lifetime);
            var target = new DeltaRecorder();

            proxy.SetSpeed(0.5f);
            proxy.Add(lifetime, target);
            parent.Tick(0.1f);

            Assert.AreEqual(0.05f, target.Deltas[0], 0.0001f);

            lifetime.Terminate();
        }

        [Test]
        public void Pause_StopsTheTime_ContinueRestoresIt()
        {
            var lifetime = new Lifetime();
            var (parent, proxy) = Create(lifetime);
            var target = new DeltaRecorder();

            proxy.Add(lifetime, target);
            proxy.Pause();
            parent.Tick(0.1f);
            proxy.Continue();
            parent.Tick(0.1f);

            Assert.AreEqual(0f, target.Deltas[0], 0.0001f);
            Assert.AreEqual(0.1f, target.Deltas[1], 0.0001f);

            lifetime.Terminate();
        }

        [Test]
        public void SetSpeed_Negative_Throws()
        {
            var lifetime = new Lifetime();
            var (_, proxy) = Create(lifetime);

            Assert.Throws<ArgumentOutOfRangeException>(() => proxy.SetSpeed(-1f));

            lifetime.Terminate();
        }

        [Test]
        public void PauseTwice_NeedsTwoContinues()
        {
            var lifetime = new Lifetime();
            var (_, proxy) = Create(lifetime);

            proxy.SetSpeed(0.5f);
            proxy.Pause();
            proxy.Pause();
            proxy.Continue();

            Assert.AreEqual(0f, proxy.Speed.Value);

            proxy.Continue();

            Assert.AreEqual(0.5f, proxy.Speed.Value);

            lifetime.Terminate();
        }

        [Test]
        public void ContinueWithoutPause_Throws()
        {
            var lifetime = new Lifetime();
            var (_, proxy) = Create(lifetime);

            Assert.Throws<InvalidOperationException>(() => proxy.Continue());

            lifetime.Terminate();
        }

        [Test]
        public void UpdatableRemovedDuringThePass_IsNotUpdated()
        {
            var lifetime = new Lifetime();
            var (parent, proxy) = Create(lifetime);

            var secondLifetime = lifetime.Child();
            var second = new DeltaRecorder();
            var first = new DeltaRecorder { OnUpdated = () => secondLifetime.Terminate() };

            proxy.Add(lifetime, first);
            proxy.Add(secondLifetime, second);
            parent.Tick(0.1f);

            Assert.AreEqual(1, first.Deltas.Count);
            Assert.AreEqual(0, second.Deltas.Count);

            lifetime.Terminate();
        }

        [Test]
        public void ThrowingUpdatable_DoesNotStopTheOthers_IsReported()
        {
            var lifetime = new Lifetime();
            var (parent, proxy) = Create(lifetime);

            var reported = new List<Exception>();
            var previous = ReactiveExceptions.Handler;
            ReactiveExceptions.Handler = reported.Add;

            try
            {
                var after = new DeltaRecorder();
                proxy.Add(lifetime, new DeltaRecorder { OnUpdated = () => throw new InvalidOperationException("first") });
                proxy.Add(lifetime, after);

                parent.Tick(0.1f);

                Assert.AreEqual(1, after.Deltas.Count);
                Assert.AreEqual(1, reported.Count);
                Assert.AreEqual("first", reported[0].Message);
            }
            finally
            {
                ReactiveExceptions.Handler = previous;
                lifetime.Terminate();
            }
        }

        private static (ManualUpdater, UpdaterProxy) Create(IReadOnlyLifetime lifetime)
        {
            var parent = new ManualUpdater();
            var proxy = new UpdaterProxy(parent);
            proxy.OnSetup(lifetime);
            return (parent, proxy);
        }

        private sealed class DeltaRecorder : IUpdatable
        {
            public readonly List<float> Deltas = new();
            public Action OnUpdated;

            public void OnUpdate(float delta)
            {
                Deltas.Add(delta);
                OnUpdated?.Invoke();
            }
        }
    }
}
