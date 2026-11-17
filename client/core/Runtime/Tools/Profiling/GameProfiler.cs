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
    /// The stack is correct while the await chain is linear. The big steps of the game (startup, meta, menu, a run) are
    /// stages (<see cref="Stage"/>), the roots of the trace. A record made while the stack is empty (the continuation of
    /// a parallel branch, a request of a session) goes to the last stage opened, even a closed one, and not to the root:
    /// the parent covers it in the trace. A branch that outlives its stage holds its own span and nests into it
    /// explicitly (<see cref="IProfilerScope.Child"/>, <see cref="Ambient"/>): the stage is the next one by then.
    /// </summary>
    [AutoStaticsCleanup]
    public static partial class GameProfiler
    {
        private static readonly List<IProfilerScope> _stack = new();
        private static IProfilerScope _stage;

        public static IProfiler Current { get; private set; }

        /// <summary>
        /// The first trace of this run of the game: its start. Stays after it finishes, with the traces of the next stages of
        /// the game going on (<see cref="Profiler.Final"/>). Null until the first <see cref="Begin"/>.
        /// </summary>
        public static Profiler Startup { get; private set; }

        public static bool IsRunning => Current != null;

        public static IProfiler Begin(string name)
        {
            _stack.Clear();
            _stage = null;

            var profiler = new Profiler(name);

            Current = profiler;
            Startup ??= profiler;

            return profiler;
        }

        public static void Finish()
        {
            if (Current == null)
                return;

            _stack.Clear();
            _stage = null;
            Current.Finish();
            Current = null;
        }

        /// <summary>Scope inside the currently open stage.</summary>
        public static IProfilerScope Scope(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            return Open(Create(name, CurrentParent));
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

            var scope = Create(name, CurrentParent);

            scope.Start();

            return scope;
        }

        /// <summary>
        /// The span opened at the current stage. Needed by code that measures its own steps
        /// after the first await: there is no stack there and nowhere to take the parent from.
        /// </summary>
        public static IProfilerScope CurrentScope => CurrentParent ?? NullProfilerScope.Instance;

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

        /// <summary>
        /// A big step of the game, a root of the trace. Stays the place of the late records after it is closed, until the
        /// next stage opens.
        /// </summary>
        public static IProfilerScope Stage(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            _stage = Current.CreateScope(name);

            return Open(_stage);
        }

        /// <summary>
        /// A branch of the current stage that runs in parallel with the main chain: a child of the stage whatever
        /// is on the stack, not on the stack itself, so unrelated code does not nest into it.
        /// </summary>
        public static IProfilerScope Detached(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            var scope = Create(name, _stage);
            scope.Start();

            return scope;
        }

        /// <summary>
        /// A branch nothing waits for (a load that is used when it is ready): a root of the trace of its own, not a stage and
        /// not on the stack, so it neither holds a stage open in the trace nor takes the late records. What it measures after
        /// its synchronous part is created inside <see cref="Ambient"/> of it.
        /// </summary>
        public static IProfilerScope Background(string name)
        {
            if (Current == null)
                return NullProfilerScope.Instance;

            var scope = Current.CreateScope(name);
            scope.Start();

            return scope;
        }

        private static IProfilerScope CurrentParent => _stack.Count > 0 ? _stack[^1] : _stage;

        private static IProfilerScope Create(string name, IProfilerScope parent)
        {
            return parent == null ? Current.CreateScope(name) : parent.Child(name);
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