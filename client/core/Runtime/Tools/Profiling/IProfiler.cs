using System;
using System.Collections.Generic;

namespace Internal
{
    /// <summary>
    /// Loading stage profiler. One per trace: created at the start of the measured
    /// process, hands out root scopes and on <see cref="Finish"/> puts the result
    /// into <see cref="ProfilerTraceStorage"/>, where the waterfall window reads it.
    /// </summary>
    public interface IProfiler
    {
        string Name { get; }

        /// <summary>Time since profiler start, ms. A single timeline for all scopes of the trace.</summary>
        double ElapsedMs { get; }

        /// <summary>Frames since the trace start.</summary>
        int ElapsedFrames { get; }

        /// <summary>Creates a root scope. It must be started manually - <see cref="IProfilerScope.Start"/>.</summary>
        IProfilerScope CreateScope(string name);

        /// <summary>Stops the trace, closes unclosed scopes and saves the result.</summary>
        void Finish();
    }

    /// <summary>
    /// A span on the trace timeline. Dispose is equivalent to <see cref="Stop"/> - handy for using.
    /// </summary>
    public interface IProfilerScope : IDisposable
    {
        string Name { get; }
        bool IsRunning { get; }

        /// <summary>Span start since profiler start, ms. -1 until the scope has started.</summary>
        double StartMs { get; }

        /// <summary>Span duration, ms. For a running scope - the time since its start.</summary>
        double DurationMs { get; }

        /// <summary>
        /// How many frames the span took. Async loading is almost always bound
        /// by frames rather than CPU, and this number shows it right away.
        /// </summary>
        int Frames { get; }

        IReadOnlyList<IProfilerScope> Children { get; }

        IProfilerScope Child(string name);

        /// <summary>
        /// Renames the span: the name of the loaded scene or asset is known only
        /// after loading has finished, while the scope is needed before it starts.
        /// </summary>
        void SetName(string name);

        void Start();
        void Stop();
    }
}