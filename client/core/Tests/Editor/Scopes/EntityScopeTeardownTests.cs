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

    public static class EntityTeardownRoots
    {
        public static AssetGroup Group { get; private set; }
        public static ILifetime ParentLifetime { get; private set; }

        public static void SetGroup(AssetGroup group)
        {
            Group = group;
        }

        public static void SetParentLifetime(ILifetime lifetime)
        {
            ParentLifetime = lifetime;
        }

        public static void Request(IEntityBuilder builder)
        {
            RetainOnBuild(builder.Events);
        }

        public static UniTask RequestAsync(IEntityBuilder builder)
        {
            RetainOnBuild(builder.Events);
            return UniTask.CompletedTask;
        }

        public static async UniTask GatedLoad(IEntityBuilder builder)
        {
            await ScopeLoaderProbe.Gate.Task;
            await RetainNow(builder.Events);
        }

        public static void EndingSetup(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("endingSetup", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntitySetup>().As<IScopeSetup>();
            builder.Register<EntityTeardownEndingSetup>().As<IScopeSetup>();
        }

        // The catalogue extensions take an IScopeBuilder; an entity reaches the same events through its builder.
        private static void RetainOnBuild(IEventLoop events)
        {
            events.AddBeforeBuild(() =>
            {
                events.AddAfterDispose(Release);
                return Group.Retain();
            });
        }

        private static UniTask RetainNow(IEventLoop events)
        {
            events.AddAfterDispose(Release);
            return Group.Retain();
        }

        private static UniTask Release()
        {
            Group.Release();
            return UniTask.CompletedTask;
        }
    }

    public sealed class EntityTeardownEndingSetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            EntityTeardownRoots.ParentLifetime.Terminate();
        }
    }
}
