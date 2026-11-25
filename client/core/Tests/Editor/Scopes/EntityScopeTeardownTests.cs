using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // An entity whose lifetime ends before or during its load: what the load retained is released, the services that
    // have been set up are torn down, and the caller gets the cancellation.
    [Category("Scopes")]
    public class EntityScopeTeardownTests
    {
        [SetUp]
        public void SetUp()
        {
            ScopeLoaderProbe.Reset();
            GeneratedRoots.Register(((Action<IEntityBuilder>)EntityTeardownRoots.Request).Method);
            GeneratedRoots.Register(((Func<IEntityBuilder, UniTask>)EntityTeardownRoots.RequestAsync).Method);
            GeneratedRoots.Register(((Func<IEntityBuilder, UniTask>)EntityTeardownRoots.GatedLoad).Method);
            GeneratedRoots.Register(((Action<IEntityBuilder>)EntityTeardownRoots.EndingSetup).Method);
        }

        [UnityTest]
        public IEnumerator ParentEnded_Request_GroupIsReleased() => UniTask.ToCoroutine(async () =>
        {
            var group = new ProbeGroup();
            EntityTeardownRoots.SetGroup(group);
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            parentLifetime.Terminate();

            var caught = await Catch(() => new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                EntityTeardownRoots.Request));

            Assert.IsInstanceOf<OperationCanceledException>(caught);
            Assert.IsFalse(group.IsLoaded, "the group stays retained by an entity that never lived");
            parent.Dispose();
        });

        [UnityTest]
        public IEnumerator ParentEnded_AsyncRequest_GroupIsReleased() => UniTask.ToCoroutine(async () =>
        {
            var group = new ProbeGroup();
            EntityTeardownRoots.SetGroup(group);
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            parentLifetime.Terminate();

            var caught = await Catch(() => new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                EntityTeardownRoots.RequestAsync));

            Assert.IsInstanceOf<OperationCanceledException>(caught);
            Assert.IsFalse(group.IsLoaded, "the group stays retained by an entity that never lived");
            parent.Dispose();
        });

        // The lifetime ends while construct waits, and construct loads a group after that: the teardown has run already.
        [UnityTest]
        public IEnumerator LifetimeEndsDuringConstruct_LoadAfterIt_GroupIsReleased() => UniTask.ToCoroutine(async () =>
        {
            var group = new ProbeGroup();
            EntityTeardownRoots.SetGroup(group);
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            ScopeLoaderProbe.SetGate(new UniTaskCompletionSource());

            var load = new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                EntityTeardownRoots.GatedLoad);

            parentLifetime.Terminate();
            ScopeLoaderProbe.Gate.TrySetResult();

            var caught = await Catch(() => load);

            Assert.IsInstanceOf<OperationCanceledException>(caught);
            Assert.IsFalse(group.IsLoaded, "the group stays retained by an entity that never lived");
            parent.Dispose();
        });

        // A service ends the entity in its own setup: the teardown waits for the setup, the services are torn down, the
        // assets released and the caller gets the cancellation.
        [UnityTest]
        public IEnumerator LifetimeEndsDuringSetup_TornDown_LoadIsCanceled() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            EntityTeardownRoots.SetParentLifetime(parentLifetime);

            var caught = await Catch(() => new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                EntityTeardownRoots.EndingSetup));

            Assert.IsInstanceOf<OperationCanceledException>(caught);
            Assert.AreEqual(1, ScopeLoaderProbe.Count("entity.dispose"), "the services set up were not torn down");
            Assert.AreEqual(1, ScopeLoaderProbe.Count("endingSetup.afterDispose"), "the asset groups were not released");
            parent.Dispose();
        });

        private static async UniTask<Exception> Catch<T>(Func<UniTask<T>> action)
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                return exception;
            }

            Assert.Fail("expected an exception");
            return null;
        }
    }
}
