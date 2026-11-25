using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Internal.Tests
{
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
}
