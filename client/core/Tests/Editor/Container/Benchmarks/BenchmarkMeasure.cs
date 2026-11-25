using System;

namespace Internal.Tests
{
    internal static class BenchmarkMeasure
    {
        // Thread counter, not process counter: the profiler's "GC Allocated In Frame" also counts other editor threads
        // (MCP bridge, debugger), and their noise of 300-700 bytes per step did not go away even in min over 11 runs.
        // On CoreCLR the counter is byte-exact; on Mono it was a stub and returned 0.
        public static BenchmarkRun Capture(Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            action();
            var ticks = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            return new BenchmarkRun(ticks, allocatedAfter - allocatedBefore);
        }
    }
}
