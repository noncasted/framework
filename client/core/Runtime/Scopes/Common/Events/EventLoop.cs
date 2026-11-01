using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
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

        // A custom phase: every listener T gets the lifetime, as in the built-in phases.
        void RunCustom<T>(IReadOnlyLifetime lifetime, Action<T, IReadOnlyLifetime> invoker);
        UniTask RunCustomAsync<T>(IReadOnlyLifetime lifetime, Func<T, IReadOnlyLifetime, UniTask> invoker);
        UniTask RunConstruct(IReadOnlyLifetime lifetime);
        UniTask RunLoaded(IReadOnlyLifetime lifetime);
        UniTask RunDispose();
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

        public UniTask RunCustomAsync<T>(IReadOnlyLifetime lifetime, Func<T, IReadOnlyLifetime, UniTask> invoker)
        {
            return InvokeAsync(
                ResolveList<T>(),
                (invoker, lifetime),
                static (l, state) => state.invoker.Invoke(l, state.lifetime));
        }

        // Phase lambdas are static: the lifetime comes as an argument, otherwise every phase is a closure with a delegate.
        public async UniTask RunConstruct(IReadOnlyLifetime lifetime)
        {
            await UniTask.SwitchToMainThread();

            Invoke(ResolveList<IScopeBaseSetup>(), lifetime, static (l, lt) => l.OnBaseSetup(lt));
            await InvokeAsync(ResolveList<IScopeBaseSetupAsync>(), lifetime, static (l, lt) => l.OnBaseSetupAsync(lt));

            Invoke(ResolveList<IScopeSetup>(), lifetime, static (l, lt) => l.OnSetup(lt));
            await InvokeAsync(ResolveList<IScopeSetupAsync>(), lifetime, static (l, lt) => l.OnSetupAsync(lt));

            Invoke(ResolveList<IScopeSetupCompletion>(), lifetime, static (l, lt) => l.OnSetupCompletion(lt));

            await InvokeAsync(
                ResolveList<IScopeSetupCompletionAsync>(),
                lifetime,
                static (l, lt) => l.OnSetupCompletionAsync(lt));
        }

        public async UniTask RunLoaded(IReadOnlyLifetime lifetime)
        {
            Invoke(ResolveList<IScopeLoaded>(), lifetime, static (l, lt) => l.OnLoaded(lt));
            await InvokeAsync(ResolveList<IScopeLoadedAsync>(), lifetime, static (l, lt) => l.OnLoadedAsync(lt));
        }

        public async UniTask RunDispose()
        {
            var errors = new List<Exception>();

            Invoke(ResolveList<IScopeDispose>(), errors, static (l, e) => DisposeIsolated(l, e));
            await InvokeAsync(ResolveList<IScopeDisposeAsync>(), errors, static (l, e) => DisposeIsolated(l, e));

            if (errors.Count == 1)
                ExceptionDispatchInfo.Capture(errors[0]).Throw();

            if (errors.Count > 1)
                throw new AggregateException("[Scope] Several dispose listeners failed", errors);
        }

        // A failed listener does not take the dispose of the others with it: saves and unsubscriptions would be lost.
        private static void DisposeIsolated(IScopeDispose listener, List<Exception> errors)
        {
            try
            {
                listener.OnDispose();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        private static async UniTask DisposeIsolated(IScopeDisposeAsync listener, List<Exception> errors)
        {
            try
            {
                await listener.OnDisposeAsync();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
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
                diagnostics.RecordAllocation(typeof(T), null, ContainerDiagnostics.AllocatedBytes() - before, false);

                return list;
            }
#endif

            return _container.ResolveAll<T>();
        }

        /// <summary>
        /// Synchronous listeners run strictly one after another, so each one goes into the trace
        /// as a separate stage and its own measurements nest inside it. Asynchronous ones
        /// start in a batch via WhenAll: their stack nesting is not built, and those
        /// that have something to measure open their own segment (see <see cref="GameProfiler.Concurrent"/>).
        /// Index-based loop: foreach over IReadOnlyList boxes the array enumerator.
        /// </summary>
        private void Invoke<T, TState>(IReadOnlyList<T> listeners, TState state, Action<T, TState> invoker)
        {
            var count = listeners.Count;

#if UNITY_EDITOR
            if (_container.Diagnostics is ContainerDiagnostics diagnostics)
            {
                for (var i = 0; i < count; i++)
                {
                    var listener = listeners[i];

                    using (GameProfiler.Scope(listener.GetType().Name))
                    {
                        var before = ContainerDiagnostics.AllocatedBytes();
                        invoker.Invoke(listener, state);
                        var bytes = ContainerDiagnostics.AllocatedBytes() - before;
                        diagnostics.RecordAllocation(typeof(T), listener.GetType(), bytes, false);
                    }
                }

                return;
            }
#endif

            if (GameProfiler.IsRunning == false)
            {
                for (var i = 0; i < count; i++)
                    invoker.Invoke(listeners[i], state);

                return;
            }

            for (var i = 0; i < count; i++)
            {
                var listener = listeners[i];

                using (GameProfiler.Scope(listener.GetType().Name))
                    invoker.Invoke(listener, state);
            }
        }

        /// <summary>
        /// Asynchronous listeners start in a batch, so each one's segment is concurrent
        /// and is not pushed on the stack. During the synchronous prologue it is still made current: before
        /// the first await the listener can grab it via <see cref="GameProfiler.CurrentScope"/>
        /// and attach its own steps to it.
        /// </summary>
        private UniTask InvokeAsync<T, TState>(
            IReadOnlyList<T> listeners,
            TState state,
            Func<T, TState, UniTask> invoker)
        {
            var count = listeners.Count;

            if (count == 0)
                return UniTask.CompletedTask;

            var tasks = new UniTask[count];

#if UNITY_EDITOR
            // For an asynchronous listener only the synchronous prologue up to the first await is measured.
            if (_container.Diagnostics is ContainerDiagnostics diagnostics)
            {
                for (var i = 0; i < count; i++)
                {
                    var listener = listeners[i];
                    var scope = GameProfiler.Concurrent(listener.GetType().Name);

                    UniTask task;

                    using (GameProfiler.Ambient(scope))
                    {
                        var before = ContainerDiagnostics.AllocatedBytes();
                        task = invoker.Invoke(listener, state);
                        var bytes = ContainerDiagnostics.AllocatedBytes() - before;
                        diagnostics.RecordAllocation(typeof(T), listener.GetType(), bytes, true);
                    }

                    tasks[i] = GameProfiler.IsRunning == true ? scope.Track(task) : task;
                }

                return UniTask.WhenAll(tasks);
            }
#endif

            if (GameProfiler.IsRunning == false)
            {
                for (var i = 0; i < count; i++)
                    tasks[i] = invoker.Invoke(listeners[i], state);

                return UniTask.WhenAll(tasks);
            }

            for (var i = 0; i < count; i++)
            {
                var listener = listeners[i];
                var scope = GameProfiler.Concurrent(listener.GetType().Name);

                UniTask task;

                using (GameProfiler.Ambient(scope))
                    task = invoker.Invoke(listener, state);

                tasks[i] = scope.Track(task);
            }

            return UniTask.WhenAll(tasks);
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