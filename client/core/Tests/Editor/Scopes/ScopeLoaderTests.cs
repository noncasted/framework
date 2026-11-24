using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    [Category("Scopes")]
    public class ScopeLoaderTests
    {
        [SetUp]
        public void SetUp()
        {
            ScopeLoaderProbe.Reset();
            GeneratedRoots.Register(((Func<IScopeBuilder, UniTask>)ScopeLoaderRoots.FailingSetup).Method);
            GeneratedRoots.Register(((Func<IScopeBuilder, UniTask>)ScopeLoaderRoots.Parent).Method);
            GeneratedRoots.Register(((Func<IScopeBuilder, UniTask>)ScopeLoaderRoots.Child).Method);
            GeneratedRoots.Register(((Action<IEntityBuilder>)ScopeEntityRoots.Entity).Method);
            GeneratedRoots.Register(((Action<IEntityBuilder>)ScopeEntityRoots.FailingEntity).Method);
            GeneratedRoots.Register(((Action<IEntityBuilder>)ScopeEntityRoots.EndingEntity).Method);
            GeneratedRoots.Register(((Action<IEntityBuilder>)ScopeEntityRoots.GatedEntity).Method);
            GeneratedRoots.Register(((Action<IEntityBuilder>)ScopeEntityRoots.RootEntity).Method);
            GeneratedRoots.Register(((Func<IEntityBuilder, UniTask>)ScopeEntityRoots.AsyncGatedEntity).Method);
        }

        // A scope that fails to load is torn down: its lifetime ends and its asset groups are released.
        [UnityTest]
        public IEnumerator Load_SetupThrows_TearsDownScope() => UniTask.ToCoroutine(async () =>
        {
            var loader = new ServiceScopeLoader();
            var caught = await Catch(() => loader.Load(new ScopeLoadOptions(null, ScopeLoaderRoots.FailingSetup)));

            Assert.AreEqual("setup failed", caught.Message);
            Assert.IsTrue(ScopeLoaderProbe.Lifetimes["failing"].IsTerminated, "scope lifetime survived the failed load");
            Assert.AreEqual(1, ScopeLoaderProbe.Count("failing.afterDispose"), "asset groups were not released");
        });

        // A root given builder.ScopeLifetime gets exactly it, and ends the entity through it: the container, then the
        // asset groups.
        [UnityTest]
        public IEnumerator Entity_RootEndsScopeLifetime_TeardownInOrder() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);

            var result = await new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                ScopeEntityRoots.RootEntity);

            var root = result.Container.Resolve<ScopeEntityRoot>();
            var container = result.Container;

            Assert.AreSame(ScopeLoaderProbe.Lifetimes["root"], root.Lifetime, "the root did not get the lifetime of WithParameter");

            root.Lifetime.Terminate();

            CollectionAssert.AreEqual(new[] { "root.afterDispose" }, ScopeLoaderProbe.Log);
            Assert.IsTrue(container.Lifetime.IsTerminated, "the container was not disposed");

            parentLifetime.Terminate();
            parent.Dispose();
        });

        // The lifetime ended while the build events waited: the container is not built, nothing would dispose it.
        [UnityTest]
        public IEnumerator Entity_LifetimeEndsDuringBuildEvents_NoContainer() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            ScopeLoaderProbe.SetGate(new UniTaskCompletionSource());

            var load = new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                ScopeEntityRoots.GatedEntity);

            parentLifetime.Terminate();
            ScopeLoaderProbe.Gate.TrySetResult();

            var caught = await Catch(() => load);

            Assert.IsInstanceOf<OperationCanceledException>(caught);
            Assert.AreEqual(0, ScopeLoaderProbe.Count("entity.setup"), "the entity was set up on a dead lifetime");
            parent.Dispose();
        });

        [UnityTest]
        public IEnumerator Entity_LifetimeEndsDuringAsyncConstruct_NoContainer() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            ScopeLoaderProbe.SetGate(new UniTaskCompletionSource());

            var load = new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                ScopeEntityRoots.AsyncGatedEntity);

            parentLifetime.Terminate();
            ScopeLoaderProbe.Gate.TrySetResult();

            var caught = await Catch(() => load);

            Assert.IsInstanceOf<OperationCanceledException>(caught);
            Assert.AreEqual(0, ScopeLoaderProbe.Count("entity.setup"), "the entity was set up on a dead lifetime");
            parent.Dispose();
        });

        [UnityTest]
        public IEnumerator Dispose_Twice_RunsTeardownOnce() => UniTask.ToCoroutine(async () =>
        {
            var loader = new ServiceScopeLoader();
            var scope = await loader.Load(new ScopeLoadOptions(null, ScopeLoaderRoots.Parent));

            await scope.Dispose();
            await scope.Dispose();

            Assert.AreEqual(1, ScopeLoaderProbe.Count("parent.dispose"));
            Assert.AreEqual(1, ScopeLoaderProbe.Count("parent.afterDispose"));
        });

        // The parent goes down with its children: they are disposed first, as a whole.
        [UnityTest]
        public IEnumerator ParentDispose_DisposesChildFirst() => UniTask.ToCoroutine(async () =>
        {
            var loader = new ServiceScopeLoader();
            var parent = await loader.Load(new ScopeLoadOptions(null, ScopeLoaderRoots.Parent));
            await loader.Load(new ScopeLoadOptions(parent, ScopeLoaderRoots.Child));

            await parent.Dispose();

            CollectionAssert.AreEqual(
                new[] { "child.dispose", "child.afterDispose", "parent.dispose", "parent.afterDispose" },
                ScopeLoaderProbe.Log);
        });

        [UnityTest]
        public IEnumerator ChildDisposedFirst_ParentDoesNotDisposeItAgain() => UniTask.ToCoroutine(async () =>
        {
            var loader = new ServiceScopeLoader();
            var parent = await loader.Load(new ScopeLoadOptions(null, ScopeLoaderRoots.Parent));
            var child = await loader.Load(new ScopeLoadOptions(parent, ScopeLoaderRoots.Child));

            await child.Dispose();
            await parent.Dispose();

            Assert.AreEqual(1, ScopeLoaderProbe.Count("child.dispose"));
            Assert.AreEqual(1, ScopeLoaderProbe.Count("child.afterDispose"));
        });

        // An entity scope is set up like a service scope, and its end reaches the listeners and releases the assets.
        [UnityTest]
        public IEnumerator Entity_SetUpThenTornDown() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            var loader = new EntityScopeLoader();

            await loader.Load(parentLifetime, parent, new ScopeTestEntityView(), ScopeEntityRoots.Entity);
            parentLifetime.Terminate();

            CollectionAssert.AreEqual(
                new[] { "entity.setup", "entity.dispose", "entity.afterDispose" },
                ScopeLoaderProbe.Log);
            parent.Dispose();
        });

        // The end of an entity reaches the listeners of its setup first, as the end of a service scope does: the container
        // and the asset groups go after them.
        [UnityTest]
        public IEnumerator Entity_Ends_SetupListenersBeforeTheContainerAndTheAssets() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);

            await new EntityScopeLoader().Load(parentLifetime, parent, new ScopeTestEntityView(), ScopeEntityRoots.EndingEntity);
            parentLifetime.Terminate();

            CollectionAssert.AreEqual(new[] { "end.containerAlive", "ending.afterDispose" }, ScopeLoaderProbe.Log);
            parent.Dispose();
        });

        [UnityTest]
        public IEnumerator Entity_SetupThrows_DisposesContainer() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            var loader = new EntityScopeLoader();

            var caught = await Catch(() => loader.Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                ScopeEntityRoots.FailingEntity));

            Assert.AreEqual("entity setup failed", caught.Message);
            Assert.IsTrue(ScopeLoaderProbe.Container.Lifetime.IsTerminated, "entity container survived the failed load");
            Assert.IsTrue(ScopeLoaderProbe.Lifetimes["failingEntity"].IsTerminated);
            Assert.AreEqual(1, ScopeLoaderProbe.Count("failingEntity.afterDispose"));
            parentLifetime.Terminate();
            parent.Dispose();
        });

        private static async UniTask<Exception> Catch(Func<UniTask> action)
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
