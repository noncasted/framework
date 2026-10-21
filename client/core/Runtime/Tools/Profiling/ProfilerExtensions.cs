using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class ProfilerExtensions
    {
        /// <summary>
        /// Measures a nested operation on a specific scope, without the stack: needed where
        /// operations start in parallel (WhenAll) and stack nesting cannot be built.
        /// </summary>
        public static async UniTask Measure(this IProfilerScope parent, string name, UniTask task)
        {
            var scope = parent.Child(name);
            scope.Start();

            try
            {
                await task;
            }
            finally
            {
                scope.Stop();
            }
        }

        public static async UniTask<T> Measure<T>(this IProfilerScope parent, string name, UniTask<T> task)
        {
            var scope = parent.Child(name);
            scope.Start();

            try
            {
                return await task;
            }
            finally
            {
                scope.Stop();
            }
        }

        /// <summary>Same, but the task is created already under the started scope.</summary>
        public static async UniTask Measure(this IProfilerScope parent, string name, Func<UniTask> factory)
        {
            var scope = parent.Child(name);
            scope.Start();

            try
            {
                await factory.Invoke();
            }
            finally
            {
                scope.Stop();
            }
        }

        public static async UniTask<T> Measure<T>(this IProfilerScope parent, string name, Func<UniTask<T>> factory)
        {
            var scope = parent.Child(name);
            scope.Start();

            try
            {
                return await factory.Invoke();
            }
            finally
            {
                scope.Stop();
            }
        }

        /// <summary>
        /// Measures a step inside which unrelated code is measured: for the duration of the wait the span
        /// is made current, so nested measurements go into it rather than into the scope's stage.
        /// Suitable only for a linear chain - siblings running in parallel would nest here too.
        /// </summary>
        public static async UniTask MeasureNested(this IProfilerScope parent, string name, Func<UniTask> factory)
        {
            var scope = parent.Child(name);
            scope.Start();

            using (GameProfiler.Ambient(scope))
            {
                try
                {
                    await factory.Invoke();
                }
                finally
                {
                    scope.Stop();
                }
            }
        }

        public static async UniTask<T> MeasureNested<T>(
            this IProfilerScope parent,
            string name,
            Func<UniTask<T>> factory)
        {
            var scope = parent.Child(name);
            scope.Start();

            using (GameProfiler.Ambient(scope))
            {
                try
                {
                    return await factory.Invoke();
                }
                finally
                {
                    scope.Stop();
                }
            }
        }

        /// <summary>
        /// Closes an already open span when the task finishes: for measurements that
        /// open in one place and finish in another.
        /// </summary>
        public static async UniTask Track(this IProfilerScope scope, UniTask task)
        {
            try
            {
                await task;
            }
            finally
            {
                scope.Stop();
            }
        }

        public static async UniTask<T> Track<T>(this IProfilerScope scope, UniTask<T> task)
        {
            try
            {
                return await task;
            }
            finally
            {
                scope.Stop();
            }
        }

        public static void Measure(this IProfilerScope parent, string name, Action action)
        {
            var scope = parent.Child(name);
            scope.Start();

            try
            {
                action.Invoke();
            }
            finally
            {
                scope.Stop();
            }
        }
    }
}