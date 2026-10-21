using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Internal.Tests
{
    internal readonly struct BenchmarkRun
    {
        public readonly long Ticks;
        public readonly long AllocatedBytes;

        public BenchmarkRun(long ticks, long allocatedBytes)
        {
            Ticks = ticks;
            AllocatedBytes = allocatedBytes;
        }
    }

    internal readonly struct BenchmarkSample
    {
        public readonly string Name;
        public readonly int Runs;
        public readonly long Ticks;
        public readonly double Milliseconds;
        public readonly double MinMilliseconds;
        public readonly double MaxMilliseconds;
        public readonly long AllocatedBytes;
        public readonly long MaxAllocatedBytes;

        public BenchmarkSample(
            string name,
            int runs,
            long ticks,
            long minTicks,
            long maxTicks,
            long allocatedBytes,
            long maxAllocatedBytes)
        {
            Name = name;
            Runs = runs;
            Ticks = ticks;
            Milliseconds = ToMilliseconds(ticks);
            MinMilliseconds = ToMilliseconds(minTicks);
            MaxMilliseconds = ToMilliseconds(maxTicks);
            AllocatedBytes = allocatedBytes;
            MaxAllocatedBytes = maxAllocatedBytes;
        }

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }
    }

    // Time is the median of runs, bytes are the minimum: the counter also sees other editor threads,
    // and extra bytes can only be added.
    internal sealed class BenchmarkSeries
    {
        public BenchmarkSeries(string name)
        {
            _name = name;
        }

        private readonly string _name;
        private readonly List<BenchmarkRun> _runs = new List<BenchmarkRun>();

        public void Add(Action action)
        {
            _runs.Add(BenchmarkMeasure.Capture(action));
        }

        public BenchmarkSample ToSample()
        {
            if (_runs.Count == 0)
                throw new InvalidOperationException(_name + " has no runs");

            var ticks = new long[_runs.Count];
            var minBytes = long.MaxValue;
            var maxBytes = long.MinValue;

            for (var i = 0; i < _runs.Count; i++)
            {
                ticks[i] = _runs[i].Ticks;
                minBytes = Math.Min(minBytes, _runs[i].AllocatedBytes);
                maxBytes = Math.Max(maxBytes, _runs[i].AllocatedBytes);
            }

            Array.Sort(ticks);

            return new BenchmarkSample(
                _name,
                ticks.Length,
                ticks[ticks.Length / 2],
                ticks[0],
                ticks[ticks.Length - 1],
                minBytes,
                maxBytes);
        }
    }

    internal sealed class BenchmarkReport
    {
        public readonly string ContainerName;
        public readonly DateTime UtcDate;
        public readonly string UnityVersion;
        public readonly string Hardware;
        public readonly int PhaseItemCount;
        public readonly BenchmarkSample Build;
        public readonly BenchmarkSample BuildResolved;
        public readonly BenchmarkSample FirstResolveAll;
        public readonly BenchmarkSample Singleton10k;
        public readonly BenchmarkSample Transient10k;

        public BenchmarkReport(
            string containerName,
            int phaseItemCount,
            BenchmarkSample build,
            BenchmarkSample buildResolved,
            BenchmarkSample firstResolveAll,
            BenchmarkSample singleton10k,
            BenchmarkSample transient10k)
        {
            ContainerName = containerName;
            UtcDate = DateTime.UtcNow;
            UnityVersion = Application.unityVersion;

            Hardware = $"{SystemInfo.processorType} ({SystemInfo.processorCount} cores), " +
                       $"{SystemInfo.systemMemorySize} MB, {SystemInfo.operatingSystem}";
            PhaseItemCount = phaseItemCount;
            Build = build;
            BuildResolved = buildResolved;
            FirstResolveAll = firstResolveAll;
            Singleton10k = singleton10k;
            Transient10k = transient10k;
        }

        public string Format()
        {
            var culture = CultureInfo.InvariantCulture;
            var builder = new StringBuilder();
            builder.AppendLine($"container: {ContainerName}");
            builder.AppendLine($"date: {UtcDate.ToString("yyyy-MM-dd HH:mm:ss", culture)} UTC");
            builder.AppendLine($"unity: {UnityVersion}");
            builder.AppendLine($"hardware: {Hardware}");

            builder.AppendLine(
                "warmup: 1 discarded full pass (Build + resolve every service + 12 ResolveAll from deepest + " +
                $"{BenchmarkGraph.ResolveIterations} singleton + {BenchmarkGraph.ResolveIterations} transient + Dispose). " +
                "JIT/domain reload is not in the numbers.");

            builder.AppendLine(
                $"runs: {Build.Runs} measured passes, fresh sessions each; GC.Collect + WaitForPendingFinalizers + " +
                "GC.Collect before every metric. Time = median (min-max), " +
                $"allocated = GC.GetAllocatedBytesForCurrentThread delta, min (max) over runs.");

            builder.AppendLine(
                $"graph: {BenchmarkGraph.Registrations.Length} services, depth {BenchmarkGraph.ScopeDepth}, " +
                $"{BenchmarkGraph.Markers.Length} marker interfaces, " +
                $"singleton={BenchmarkGraph.SingletonResolveType.Name}, " +
                $"transient={BenchmarkGraph.TransientResolveType.Name}, " +
                $"first ResolveAll item count={PhaseItemCount}");
            builder.AppendLine();
            builder.AppendLine("| Metric | Median (ms) | Min-max (ms) | Median ticks | Allocated bytes (min / max) |");
            builder.AppendLine("|---|---|---|---|---|");
            AppendRow(builder, Build, culture);
            AppendRow(builder, BuildResolved, culture);
            AppendRow(builder, FirstResolveAll, culture);
            AppendRow(builder, Singleton10k, culture);
            AppendRow(builder, Transient10k, culture);
            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, BenchmarkSample sample, CultureInfo culture)
        {
            builder.Append("| ");
            builder.Append(sample.Name);
            builder.Append(" | ");
            builder.Append(sample.Milliseconds.ToString("0.0000", culture));
            builder.Append(" | ");
            builder.Append(sample.MinMilliseconds.ToString("0.0000", culture));
            builder.Append(" - ");
            builder.Append(sample.MaxMilliseconds.ToString("0.0000", culture));
            builder.Append(" | ");
            builder.Append(sample.Ticks.ToString(culture));
            builder.Append(" | ");
            builder.Append(sample.AllocatedBytes.ToString(culture));
            builder.Append(" / ");
            builder.Append(sample.MaxAllocatedBytes.ToString(culture));
            builder.AppendLine(" |");
        }
    }

    internal interface IBenchmarkSession : IDisposable
    {
        int ResolveAllPhases();
        object ResolveSingleton();
        object ResolveTransient();
        object Resolve(BenchmarkScopeLevel level, Type type);
    }

    internal static class ContainerBenchmarkRunner
    {
        // Odd number: the median is one of the real measurements.
        public const int Runs = 11;

        public static BenchmarkReport Run(string containerName, Func<IBenchmarkSession> open)
        {
            using (var warmup = open())
            {
                Keep(ResolveGraph(warmup));
                Keep(warmup.ResolveAllPhases());
                RunLoop(warmup.ResolveSingleton);
                RunLoop(warmup.ResolveTransient);
            }

            var build = new BenchmarkSeries("Build");

            var buildResolved = new BenchmarkSeries(
                $"Build + resolve all {BenchmarkGraph.Registrations.Length} services");
            var firstResolveAll = new BenchmarkSeries("First ResolveAll of 12 marker phases");
            var singleton = new BenchmarkSeries("10k Resolve singleton from deepest");
            var transient = new BenchmarkSeries("10k Transient");
            var phaseItemCount = 0;

            for (var run = 0; run < Runs; run++)
            {
                // Separate session: after a full resolve all VContainer instances are already created,
                // and the remaining measurements would no longer be comparable with previous runs.
                IBenchmarkSession resolved = null;

                buildResolved.Add(() => {
                    resolved = open();
                    Keep(ResolveGraph(resolved));
                });
                resolved?.Dispose();

                IBenchmarkSession session = null;
                build.Add(() => session = open());

                try
                {
                    // Delegates are created before measuring, otherwise their bytes would get into the metric.
                    Func<object> resolveSingleton = session.ResolveSingleton;
                    Func<object> resolveTransient = session.ResolveTransient;
                    firstResolveAll.Add(() => phaseItemCount = session.ResolveAllPhases());
                    singleton.Add(() => RunLoop(resolveSingleton));
                    transient.Add(() => RunLoop(resolveTransient));
                }
                finally
                {
                    session?.Dispose();
                }
            }

            return new BenchmarkReport(
                containerName,
                phaseItemCount,
                build.ToSample(),
                buildResolved.ToSample(),
                firstResolveAll.ToSample(),
                singleton.ToSample(),
                transient.ToSample());
        }

        // Each service from its own level. VContainer creates instances lazily, on resolve;
        // the generated class creates singleton/scoped already in the scope constructor.
        private static int ResolveGraph(IBenchmarkSession session)
        {
            var registrations = BenchmarkGraph.Registrations;

            for (var i = 0; i < registrations.Length; i++)
            {
                var registration = registrations[i];

                if (session.Resolve(registration.Scope, registration.Implementation) == null)
                    throw new InvalidOperationException("Resolve returned null for " +
                                                        registration.Implementation.Name);
            }

            return registrations.Length;
        }

        private static void RunLoop(Func<object> resolve)
        {
            object sink = null;

            for (var i = 0; i < BenchmarkGraph.ResolveIterations; i++)
                sink = resolve();

            if (sink == null)
                throw new InvalidOperationException("Resolve returned null");
        }

        private static void Keep(int count)
        {
            if (count < 0)
                throw new InvalidOperationException("ResolveAll returned a negative count");
        }
    }

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