using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    /// <summary>
    /// Access point to the loading trace. Loading stages are spread across static
    /// scope extensions, where the profiler cannot be passed through DI, so the current
    /// trace lives in a static, and nesting is kept by a stack of open scopes.
    ///
    /// The stack is correct while the await chain is linear. Branches that go
    /// parallel (fire-and-forget) are raised through <see cref="Branch"/> - they
    /// land in the trace root and do not affect other nesting.
    /// </summary>
    [AutoStaticsCleanup]
    public static partial class GameProfiler
    {
        private static readonly List<IProfilerScope> _stack = new();

        public static IProfiler Current { get; private set; }

        public static bool IsRunning => Current != null;

        public static IProfiler Begin(string name)
        {
            _stack.Clear();
            Current = new Profiler(name);

            return Current;
        }

        public static void Finish()
        {
            if (Current == null)
                return;

            _stack.Clear();
            Current.Finish();
            Current = null;
        }

        /// <summary>Scope inside the currently open stage.</summary>
        public static IProfilerScope Scope(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            var parent = _stack.Count > 0 ? _stack[^1] : null;
            var scope = parent == null ? Current.CreateScope(name) : parent.Child(name);

            return Open(scope);
        }

        /// <summary>
        /// A span that runs in parallel with its siblings: nested into the current stage, but it
        /// does not go on the stack itself. Otherwise a sibling load started by the same WhenAll would nest
        /// into it and the trace would lie about causality.
        /// </summary>
        public static IProfilerScope Concurrent(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            var parent = _stack.Count > 0 ? _stack[^1] : null;
            var scope = parent == null ? Current.CreateScope(name) : parent.Child(name);

            scope.Start();

            return scope;
        }

        /// <summary>
        /// The span opened at the current stage. Needed by code that measures its own steps
        /// after the first await: there is no stack there and nowhere to take the parent from.
        /// </summary>
        public static IProfilerScope CurrentScope => _stack.Count > 0 ? _stack[^1] : NullProfilerScope.Instance;

        /// <summary>
        /// Makes an already open span current. Needed for parallel branches: while their
        /// synchronous prologue runs, nested measurements must go into their own branch, not a sibling one.
        /// </summary>
        public static IDisposable Ambient(IProfilerScope scope)
        {
            if (Current == null || scope is NullProfilerScope)
                return NullAmbient.Instance;

            _stack.Add(scope);

            return new AmbientHandle(scope);
        }

        /// <summary>Scope at the trace root: for branches that run in parallel with the main chain.</summary>
        public static IProfilerScope Branch(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            return Open(Current.CreateScope(name));
        }

        /// <summary>
        /// Root scope that does not go on the stack: measures a span that unrelated
        /// code must not nest into.
        /// </summary>
        public static IProfilerScope Detached(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            var scope = Current.CreateScope(name);
            scope.Start();

            return scope;
        }

        private static IProfilerScope Open(IProfilerScope scope)
        {
            scope.Start();
            _stack.Add(scope);

            return new AmbientScope(scope);
        }

        private static void Close(IProfilerScope scope)
        {
            var index = _stack.LastIndexOf(scope);

            if (index >= 0)
                _stack.RemoveAt(index);
        }

        /// <summary>Pops the span from the stack without stopping it: its owner stops it.</summary>
        private class AmbientHandle : IDisposable
        {
            public AmbientHandle(IProfilerScope scope)
            {
                _scope = scope;
            }

            private readonly IProfilerScope _scope;

            public void Dispose()
            {
                Close(_scope);
            }
        }

        [NoAutoStaticsCleanup]
        private class NullAmbient : IDisposable
        {
            public static readonly NullAmbient Instance = new();

            public void Dispose()
            {
            }
        }

        /// <summary>
        /// Wrapper that pops the scope from the stack on Dispose. Removes by reference, not
        /// from the top: parallel branches close in a different order than they opened.
        /// </summary>
        private class AmbientScope : IProfilerScope
        {
            public AmbientScope(IProfilerScope scope)
            {
                _scope = scope;
            }

            private readonly IProfilerScope _scope;

            public string Name => _scope.Name;
            public bool IsRunning => _scope.IsRunning;
            public double StartMs => _scope.StartMs;
            public double DurationMs => _scope.DurationMs;
            public int Frames => _scope.Frames;
            public IReadOnlyList<IProfilerScope> Children => _scope.Children;

            public IProfilerScope Child(string name) => _scope.Child(name);

            public void SetName(string name) => _scope.SetName(name);

            public void Start() => _scope.Start();

            public void Stop()
            {
                _scope.Stop();
                Close(_scope);
            }

            public void Dispose()
            {
                Stop();
            }
        }
    }

    [NoAutoStaticsCleanup]
    public class NullProfilerScope : IProfilerScope
    {
        public static readonly NullProfilerScope Instance = new();

        private static readonly IProfilerScope[] _children = new IProfilerScope[0];

        public string Name => string.Empty;
        public bool IsRunning => false;
        public double StartMs => 0d;
        public double DurationMs => 0d;
        public int Frames => 0;
        public IReadOnlyList<IProfilerScope> Children => _children;

        public IProfilerScope Child(string name) => Instance;

        public void SetName(string name)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}