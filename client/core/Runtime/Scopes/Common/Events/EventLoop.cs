using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IEventLoop
    {
        void AddBeforeBuild(Func<UniTask> callback);
        // Runs after the scope is fully torn down (lifetime terminated, scenes unloaded, container disposed):
        // asset groups are released here, so teardown listeners may still read the assets of the scope.
        void AddAfterDispose(Func<UniTask> callback);

        UniTask InvokeBeforeBuild();
        UniTask InvokeAfterDispose();

        void Bind(IContainer container);

        // An event of the owner of the scope (the game started, the players created): every registration of T is called
        // with the lifetime, at the moment the owner chooses.
        void RunCustom<T>(IReadOnlyLifetime lifetime, Action<T, IReadOnlyLifetime> invoker);

        // The setup of the scope: every IScopeSetup of it, once the container is built.
        UniTask RunSetup(IReadOnlyLifetime lifetime);
    }

    public class EventLoop : IEventLoop
    {
        // Callbacks are needed only by scopes that load assets: the lists are created on the first Add.
        private List<Func<UniTask>> _beforeBuildCallbacks;
        private List<Func<UniTask>> _afterDisposeCallbacks;

        private IContainer _container;

        public void AddBeforeBuild(Func<UniTask> callback)
        {
            _beforeBuildCallbacks ??= new List<Func<UniTask>>();
            _beforeBuildCallbacks.Add(callback);
        }

        public void AddAfterDispose(Func<UniTask> callback)
        {
            _afterDisposeCallbacks ??= new List<Func<UniTask>>();
            _afterDisposeCallbacks.Add(callback);
        }

        public async UniTask InvokeBeforeBuild()
        {
            await InvokeCallbacks(_beforeBuildCallbacks);
            _beforeBuildCallbacks?.Clear();
        }

        public async UniTask InvokeAfterDispose()
        {
            await InvokeCallbacks(_afterDisposeCallbacks);
            _afterDisposeCallbacks?.Clear();
        }

        public void Bind(IContainer container)
        {
            _container = container;
        }

        public void RunCustom<T>(IReadOnlyLifetime lifetime, Action<T, IReadOnlyLifetime> invoker)
        {
            Invoke(ResolveList<T>(), (invoker, lifetime), static (l, state) => state.invoker.Invoke(l, state.lifetime));
        }

        // Phase lambdas are static: the lifetime comes as an argument, otherwise every phase is a closure with a delegate.
        public async UniTask RunSetup(IReadOnlyLifetime lifetime)
        {
            await UniTask.SwitchToMainThread();

            Invoke(ResolveList<IScopeSetup>(), lifetime, static (l, lt) => l.OnSetup(lt));
        }

        /// <summary>
        /// Resolving the list also creates all the services of the stage, so it is measured
        /// separately from their own callbacks: otherwise the construction cost gets lost.
        /// The segment name is built only under the profiler: it is a string per phase of every scope.
        /// </summary>
        private IReadOnlyList<T> ResolveList<T>()
        {
            if (GameProfiler.IsRunning == false)
                return ResolveMeasured<T>();

            using (GameProfiler.Scope($"Resolve: {typeof(T).Name}"))
                return ResolveMeasured<T>();
        }

        // In the editor with diagnostics enabled, the bytes of the resolve and of each listener are written to the container
        // (Container Debugger window). Without diagnostics and in the build the path is unchanged.
        private IReadOnlyList<T> ResolveMeasured<T>()
        {
#if UNITY_EDITOR
            if (_container.Diagnostics is ContainerDiagnostics diagnostics)
            {
                var before = ContainerDiagnostics.AllocatedBytes();
                var list = _container.ResolveAll<T>();
                diagnostics.RecordAllocation(typeof(T), null, ContainerDiagnostics.AllocatedBytes() - before);

                return list;
            }
#endif

            return _container.ResolveAll<T>();
        }

        /// <summary>
        /// Listeners run strictly one after another, so each one goes into the trace
        /// as a separate stage and its own measurements nest inside it.
        /// Index-based loop: foreach over IReadOnlyList boxes the array enumerator.
        /// </summary>
        private void Invoke<T, TState>(IReadOnlyList<T> listeners, TState state, Action<T, TState> invoker)
        {
            var count = listeners.Count;
            var diagnostics = FindDiagnostics();
            var isMeasured = diagnostics != null || GameProfiler.IsRunning == true;

            for (var i = 0; i < count; i++)
            {
                var listener = listeners[i];

                if (isMeasured == false)
                {
                    invoker.Invoke(listener, state);
                    continue;
                }

                using (GameProfiler.Scope(listener.GetType().Name))
                {
                    var before = diagnostics != null ? ContainerDiagnostics.AllocatedBytes() : 0;
                    invoker.Invoke(listener, state);
                    diagnostics?.RecordAllocation(typeof(T), listener.GetType(), ContainerDiagnostics.AllocatedBytes() - before);
                }
            }
        }

        // In the editor with diagnostics enabled; null without them and in a build.
        private ContainerDiagnostics FindDiagnostics()
        {
#if UNITY_EDITOR
            return _container.Diagnostics as ContainerDiagnostics;
#else
            return null;
#endif
        }

        private static UniTask InvokeCallbacks(List<Func<UniTask>> callbacks)
        {
            if (callbacks == null || callbacks.Count == 0)
                return UniTask.CompletedTask;

            var tasks = new UniTask[callbacks.Count];

            for (var i = 0; i < callbacks.Count; i++)
                tasks[i] = callbacks[i].Invoke();

            return UniTask.WhenAll(tasks);
        }
    }
}