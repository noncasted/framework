using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Scripting.LifecycleManagement;

namespace Internal.Tests
{
    // Records what the scopes of the loader tests went through, in order.
    [NoAutoStaticsCleanup]
    public static class ScopeLoaderProbe
    {
        public static readonly List<string> Log = new List<string>();
        public static readonly Dictionary<string, IReadOnlyLifetime> Lifetimes = new Dictionary<string, IReadOnlyLifetime>();
        public static IContainer Container { get; private set; }
        public static UniTaskCompletionSource Gate { get; private set; }

        public static void Reset()
        {
            Log.Clear();
            Lifetimes.Clear();
            Container = null;
            Gate = null;
        }

        public static void SetContainer(IContainer container)
        {
            Container = container;
        }

        public static void SetGate(UniTaskCompletionSource gate)
        {
            Gate = gate;
        }

        public static int Count(string entry)
        {
            var count = 0;

            for (var i = 0; i < Log.Count; i++)
            {
                if (Log[i] == entry)
                    count++;
            }

            return count;
        }

        // The asset groups of a scope are released the same way: an after-dispose callback of its event loop.
        public static void Track(string name, IEventLoop events, IReadOnlyLifetime lifetime)
        {
            Lifetimes[name] = lifetime;
            events.AddAfterDispose(() =>
            {
                Log.Add(name + ".afterDispose");
                return UniTask.CompletedTask;
            });
        }
    }

    public static class ScopeLoaderRoots
    {
        [ContainerGraphRoot]
        public static UniTask FailingSetup(IScopeBuilder builder)
        {
            ScopeLoaderProbe.Track("failing", builder.Events, builder.Lifetime);
            builder.Register<ScopeLoaderThrowingSetup>().As<IScopeSetup>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask Parent(IScopeBuilder builder)
        {
            ScopeLoaderProbe.Track("parent", builder.Events, builder.Lifetime);
            builder.Register<ScopeLoaderParentTeardown>().As<IScopeSetup>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        [ContainerScopeParent(typeof(ScopeLoaderRoots), nameof(Parent))]
        public static UniTask Child(IScopeBuilder builder)
        {
            ScopeLoaderProbe.Track("child", builder.Events, builder.Lifetime);
            builder.Register<ScopeLoaderChildTeardown>().As<IScopeSetup>();
            return UniTask.CompletedTask;
        }
    }

    // An asset group of the tests: loaded at once; its unload may fail.
    public sealed class ProbeGroup : AssetGroup
    {
        public ProbeGroup(bool failsToUnload = false)
        {
            _failsToUnload = failsToUnload;
        }

        private readonly bool _failsToUnload;

        protected override UniTask LoadGroup()
        {
            return UniTask.CompletedTask;
        }

        protected override void UnloadGroup()
        {
            if (_failsToUnload == true)
                throw new InvalidOperationException("unload failed");
        }
    }

    public static class ScopeEntityRoots
    {
        public static void Entity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("entity", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntitySetup>().As<IScopeSetup>();
        }

        // A service that reacts to the end of the entity: it may still use the container and the assets.
        public static void EndingEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("ending", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntityEndListener>().As<IScopeSetup>();
        }

        public static void FailingEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("failingEntity", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntityThrowingSetup>().As<IScopeSetup>();
        }

        // The root of a game: it ends the entity through the lifetime it is given.
        public static void RootEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("root", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntityRoot>().WithParameter(builder.ScopeLifetime).AsSelf();
        }

        // The build events wait: the lifetime may end while the assets of the entity load.
        public static void GatedEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("gated", builder.Events, builder.Lifetime);
            builder.Events.AddBeforeBuild(() => ScopeLoaderProbe.Gate.Task);
            builder.Register<ScopeEntitySetup>().As<IScopeSetup>();
        }

        public static async UniTask AsyncGatedEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("asyncGated", builder.Events, builder.Lifetime);
            await ScopeLoaderProbe.Gate.Task;
            builder.Register<ScopeEntitySetup>().As<IScopeSetup>();
        }
    }

    public sealed class ScopeLoaderThrowingSetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            throw new InvalidOperationException("setup failed");
        }
    }

    // Records the end of its scope; the record of the profiler shows the stage the teardown of the scope goes to.
    public sealed class ScopeLoaderParentTeardown : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            lifetime.Listen(() => {
                GameProfiler.Scope("Parent teardown").Dispose();
                ScopeLoaderProbe.Log.Add("parent.dispose");
            });
        }
    }

    public sealed class ScopeLoaderChildTeardown : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            lifetime.Listen(() => ScopeLoaderProbe.Log.Add("child.dispose"));
        }
    }

    public sealed class ScopeEntitySetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            ScopeLoaderProbe.Log.Add("entity.setup");
            lifetime.Listen(() => ScopeLoaderProbe.Log.Add("entity.dispose"));
        }
    }

    public sealed class ScopeEntityEndListener : IScopeSetup
    {
        public ScopeEntityEndListener(IContainer container)
        {
            _container = container;
        }

        private readonly IContainer _container;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            lifetime.Listen(() => ScopeLoaderProbe.Log.Add(
                _container.Lifetime.IsTerminated == true ? "end.containerOver" : "end.containerAlive"));
        }
    }

    public sealed class ScopeEntityRoot
    {
        public ScopeEntityRoot(ILifetime lifetime)
        {
            Lifetime = lifetime;
        }

        public ILifetime Lifetime { get; }
    }

    public sealed class ScopeEntityThrowingSetup : IScopeSetup
    {
        public ScopeEntityThrowingSetup(IContainer container)
        {
            ScopeLoaderProbe.SetContainer(container);
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            throw new InvalidOperationException("entity setup failed");
        }
    }

    // A plain entity view: no prefab, no components.
    public sealed class ScopeTestEntityView : IScopeEntityView
    {
        public void CreateViews(IEntityBuilder builder)
        {
        }

        public void Bind(IContainer container)
        {
        }
    }
}
