using System;

namespace Internal.Tests
{
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
}