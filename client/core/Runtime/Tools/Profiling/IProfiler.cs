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
}