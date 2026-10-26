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
        public static IContainer Container;

        public static void Reset()
        {
            Log.Clear();
            Lifetimes.Clear();
            Container = null;
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
        public static UniTask ThrowingDispose(IScopeBuilder builder)
        {
            ScopeLoaderProbe.Track("throwing", builder.Events, builder.Lifetime);
            builder.Register<ScopeLoaderThrowingDispose>().As<IScopeDispose>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        public static UniTask Parent(IScopeBuilder builder)
        {
            ScopeLoaderProbe.Track("parent", builder.Events, builder.Lifetime);
            builder.Register<ScopeLoaderParentDispose>().As<IScopeDispose>();
            return UniTask.CompletedTask;
        }

        [ContainerGraphRoot]
        [ContainerScopeParent(typeof(ScopeLoaderRoots), nameof(Parent))]
        public static UniTask Child(IScopeBuilder builder)
        {
            ScopeLoaderProbe.Track("child", builder.Events, builder.Lifetime);
            builder.Register<ScopeLoaderChildDispose>().As<IScopeDispose>();
            return UniTask.CompletedTask;
        }
    }

    public static class ScopeEntityRoots
    {
        public static void Entity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("entity", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntityLoaded>().As<IScopeLoaded>();
            builder.Register<ScopeEntityDispose>().As<IScopeDispose>();
        }

        public static void FailingEntity(IEntityBuilder builder)
        {
            ScopeLoaderProbe.Track("failingEntity", builder.Events, builder.Lifetime);
            builder.Register<ScopeEntityThrowingSetup>().As<IScopeSetup>();
        }
    }

    public sealed class ScopeLoaderThrowingSetup : IScopeSetup
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            throw new InvalidOperationException("setup failed");
        }
    }

    public sealed class ScopeLoaderThrowingDispose : IScopeDispose
    {
        public void OnDispose()
        {
            ScopeLoaderProbe.Log.Add("throwing.dispose");
            throw new InvalidOperationException("dispose failed");
        }
    }

    public sealed class ScopeLoaderParentDispose : IScopeDispose
    {
        public void OnDispose()
        {
            ScopeLoaderProbe.Log.Add("parent.dispose");
        }
    }

    public sealed class ScopeLoaderChildDispose : IScopeDispose
    {
        public void OnDispose()
        {
            ScopeLoaderProbe.Log.Add("child.dispose");
        }
    }

    public sealed class ScopeEntityLoaded : IScopeLoaded
    {
        public void OnLoaded(IReadOnlyLifetime lifetime)
        {
            ScopeLoaderProbe.Log.Add("entity.loaded");
        }
    }

    public sealed class ScopeEntityDispose : IScopeDispose
    {
        public void OnDispose()
        {
            ScopeLoaderProbe.Log.Add("entity.dispose");
        }
    }

    public sealed class ScopeEntityThrowingSetup : IScopeSetup
    {
        public ScopeEntityThrowingSetup(IContainer container)
        {
            ScopeLoaderProbe.Container = container;
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
