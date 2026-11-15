using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // An entity whose lifetime ends before or during its load: what the load retained is released, the services that
    // have started their setup are disposed, and the caller gets the cancellation.
    [Category("Scopes")]
    public class EntityScopeTeardownTests
    {
        [SetUp]
        public void SetUp()
        {
            ScopeLoaderProbe.Reset();
            GeneratedScopeHarness.Register(((Action<IEntityBuilder>)EntityTeardownRoots.Request).Method);
            GeneratedScopeHarness.Register(((Func<IEntityBuilder, UniTask>)EntityTeardownRoots.RequestAsync).Method);
            GeneratedScopeHarness.Register(((Func<IEntityBuilder, UniTask>)EntityTeardownRoots.GatedLoad).Method);
            GeneratedScopeHarness.Register(((Action<IEntityBuilder>)EntityTeardownRoots.GatedSetup).Method);
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

        // The lifetime ends while an async setup waits: the dispose phase runs, the container outlives the setup.
        [UnityTest]
        public IEnumerator LifetimeEndsDuringSetup_DisposePhaseRuns_LoadIsCanceled() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            ScopeLoaderProbe.SetGate(new UniTaskCompletionSource());

            var load = new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                EntityTeardownRoots.GatedSetup);

            parentLifetime.Terminate();
            ScopeLoaderProbe.Gate.TrySetResult();

            var caught = await Catch(() => load);

            Assert.IsInstanceOf<OperationCanceledException>(caught);
            Assert.AreEqual(1, ScopeLoaderProbe.Count("entity.dispose"), "the services set up were not disposed");
            Assert.AreEqual(1, ScopeLoaderProbe.Count("gatedSetup.afterDispose"), "the asset groups were not released");
            Assert.AreEqual(0, ScopeLoaderProbe.Count("entity.loaded"), "the entity was loaded on a dead lifetime");
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

        public static void SetGroup(AssetGroup group)
        {
            Group = group;
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

        public static void GatedSetup(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("gatedSetup", builder.Events, builder.Lifetime);
            builder.Register<EntityTeardownGatedSetup>().As<IScopeSetupAsync>();
            builder.Register<ScopeEntityDispose>().As<IScopeDispose>();
            builder.Register<ScopeEntityLoaded>().As<IScopeLoaded>();
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

    public sealed class EntityTeardownGatedSetup : IScopeSetupAsync
    {
        public UniTask OnSetup(IReadOnlyLifetime lifetime)
        {
            return ScopeLoaderProbe.Gate.Task;
        }
    }
}
