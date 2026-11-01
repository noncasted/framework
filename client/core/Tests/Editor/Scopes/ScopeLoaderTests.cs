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
            GeneratedScopeHarness.Register(((Func<IScopeBuilder, UniTask>)ScopeLoaderRoots.FailingSetup).Method);
            GeneratedScopeHarness.Register(((Func<IScopeBuilder, UniTask>)ScopeLoaderRoots.ThrowingDispose).Method);
            GeneratedScopeHarness.Register(((Func<IScopeBuilder, UniTask>)ScopeLoaderRoots.Parent).Method);
            GeneratedScopeHarness.Register(((Func<IScopeBuilder, UniTask>)ScopeLoaderRoots.Child).Method);
            GeneratedScopeHarness.Register(((Action<IEntityBuilder>)ScopeEntityRoots.Entity).Method);
            GeneratedScopeHarness.Register(((Action<IEntityBuilder>)ScopeEntityRoots.FailingEntity).Method);
            GeneratedScopeHarness.Register(((Action<IEntityBuilder>)ScopeEntityRoots.GatedEntity).Method);
            GeneratedScopeHarness.Register(((Action<IEntityBuilder>)ScopeEntityRoots.RootEntity).Method);
            GeneratedScopeHarness.Register(((Func<IEntityBuilder, UniTask>)ScopeEntityRoots.AsyncGatedEntity).Method);
            GeneratedScopeHarness.Register(((Func<IScopeBuilder, UniTask>)ScopeLoaderRoots.SeveralDisposeListeners).Method);
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

        // A throwing dispose listener does not stop the teardown, and the error still reaches the caller.
        [UnityTest]
        public IEnumerator Dispose_ListenerThrows_TeardownCompletes() => UniTask.ToCoroutine(async () =>
        {
            var loader = new ServiceScopeLoader();
            var scope = await loader.Load(new ScopeLoadOptions(null, ScopeLoaderRoots.ThrowingDispose));
            var container = scope.Container;

            var caught = await Catch(() => scope.Dispose());

            Assert.AreEqual("dispose failed", caught.Message);
            Assert.IsTrue(scope.Lifetime.IsTerminated, "scope lifetime survived the dispose");
            Assert.IsTrue(container.Lifetime.IsTerminated, "container was not disposed");
            Assert.AreEqual(1, ScopeLoaderProbe.Count("throwing.afterDispose"), "asset groups were not released");
        });

        // Every dispose listener runs whatever another one has thrown; the errors reach the caller at the end.
        [UnityTest]
        public IEnumerator Dispose_ListenerThrows_OtherListenersRun() => UniTask.ToCoroutine(async () =>
        {
            var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, ScopeLoaderRoots.SeveralDisposeListeners));

            var caught = await Catch(() => scope.Dispose());

            Assert.AreEqual(1, ScopeLoaderProbe.Count("second.dispose"), "a throwing OnDispose stopped the next OnDispose");
            Assert.AreEqual(1, ScopeLoaderProbe.Count("async.dispose"), "a throwing OnDispose stopped OnDispose");
            StringAssert.Contains("dispose failed", caught.ToString());
            StringAssert.Contains("async dispose failed", caught.ToString());
        });

        // A root given builder.ScopeLifetime gets exactly it, and ends the entity through it: the dispose phase, the
        // container, then the asset groups. The container is still alive during the dispose phase.
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

            CollectionAssert.AreEqual(new[] { "root.dispose.containerAlive", "root.afterDispose" }, ScopeLoaderProbe.Log);
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
            ScopeLoaderProbe.Gate = new UniTaskCompletionSource();

            var load = new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                ScopeEntityRoots.GatedEntity);

            parentLifetime.Terminate();
            ScopeLoaderProbe.Gate.TrySetResult();

            var caught = await Catch(() => load);

            Assert.IsInstanceOf<OperationCanceledException>(caught);
            Assert.AreEqual(0, ScopeLoaderProbe.Count("entity.loaded"), "the entity was set up on a dead lifetime");
            parent.Dispose();
        });

        [UnityTest]
        public IEnumerator Entity_LifetimeEndsDuringAsyncConstruct_NoContainer() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            ScopeLoaderProbe.Gate = new UniTaskCompletionSource();

            var load = new EntityScopeLoader().Load(
                parentLifetime,
                parent,
                new ScopeTestEntityView(),
                ScopeEntityRoots.AsyncGatedEntity);

            parentLifetime.Terminate();
            ScopeLoaderProbe.Gate.TrySetResult();

            var caught = await Catch(() => load);

            Assert.IsInstanceOf<OperationCanceledException>(caught);
            Assert.AreEqual(0, ScopeLoaderProbe.Count("entity.loaded"), "the entity was set up on a dead lifetime");
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

        // An entity scope goes through the same phases as a service scope: loaded, disposed, assets released.
        [UnityTest]
        public IEnumerator Entity_RunsLoadedAndDisposePhases() => UniTask.ToCoroutine(async () =>
        {
            var parentLifetime = new Lifetime();
            var parent = GeneratedScopeHarness.Build(ScopeRuntimeRoots.Empty);
            var loader = new EntityScopeLoader();

            await loader.Load(parentLifetime, parent, new ScopeTestEntityView(), ScopeEntityRoots.Entity);
            parentLifetime.Terminate();

            CollectionAssert.AreEqual(
                new[] { "entity.loaded", "entity.dispose", "entity.afterDispose" },
                ScopeLoaderProbe.Log);
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
