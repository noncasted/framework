using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface ILoadedScope
    {
        IContainer Container { get; }
        IReadOnlyLifetime Lifetime { get; }

        UniTask Initialize();
        UniTask Dispose();
    }

    public class LoadedScope : ILoadedScope
    {
        public LoadedScope(
            IContainer container,
            ILifetime lifetime,
            IEventLoop eventLoop,
            IReadOnlyList<ILoadedScene> scenes)
        {
            _scopeLifetime = lifetime;
            _scenes = scenes;
            Container = container;
            Lifetime = lifetime;
            EventLoop = eventLoop;
        }

        private readonly ILifetime _scopeLifetime;
        private readonly IReadOnlyList<ILoadedScene> _scenes;

        // Scopes loaded with this one as the parent: they go down before it.
        private readonly List<LoadedScope> _children = new List<LoadedScope>();

        private LoadedScope _parent;

        // Kept so that a repeated Dispose waits for the same teardown instead of running it again.
        private UniTask? _disposing;

        public IContainer Container { get; }
        public IReadOnlyLifetime Lifetime { get; }
        public IEventLoop EventLoop { get; }

        public UniTask Initialize()
        {
            return EventLoop.RunLoaded(Lifetime);
        }

        public UniTask Dispose()
        {
            _disposing ??= DisposeOnce().Preserve();
            return _disposing.Value;
        }

        // The child is disposed together with this scope unless it is disposed first.
        internal void AddChild(LoadedScope child)
        {
            if (_disposing != null)
                throw new InvalidOperationException("Cannot load a child of a disposed scope.");

            child._parent = this;
            _children.Add(child);
        }

        private async UniTask DisposeOnce()
        {
            _parent?._children.Remove(this);

            var teardown = new ScopeTeardown();

            // The last loaded child goes first. A child removes itself from the list, so the list is copied.
            var children = _children.ToArray();

            for (var i = children.Length - 1; i >= 0; i--)
                await teardown.StepAwaited(children[i].Dispose);

            await teardown.StepAwaited(EventLoop.RunDispose);
            teardown.Step(_scopeLifetime.Terminate);

            // On quit Unity unloads the scenes itself, and Addressables no longer knows their handles.
            if (ApplicationQuit.IsQuitting == false)
                await teardown.StepAwaited(() => _scenes.InvokeAwaited(scene => scene.Unload()));

            teardown.Step(Container.Dispose);

            // Asset groups go last: terminating the lifetime tears down entities and views in an order that is
            // not tied to their assets (session entities die before remote users, a weapon resets the arms of its
            // still alive holder), and every such listener may read the catalogues of the scope.
            await teardown.StepAwaited(EventLoop.InvokeAfterDispose);

            teardown.ThrowErrors();
        }
    }


    public static class ServiceLoadResultExtensions
    {
        public static T Resolve<T>(this ILoadedScope loadResult)
        {
            return loadResult.Container.Resolve<T>();
        }
    }
}
