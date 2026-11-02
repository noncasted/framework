using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.Tests
{
    [Category("Container")]
    public class ContainerBenchmarkTests
    {
        // The allocation counter has failed before (GetAllocatedBytesForCurrentThread = 0 in Unity Mono):
        // check against a known size. Minimum of several measurements - because of other editor threads.
        [Test]
        public void Measure_CountsManagedAllocations()
        {
            object sink = null;
            var empty = long.MaxValue;
            var array = long.MaxValue;

            for (var i = 0; i < 5; i++)
            {
                empty = Math.Min(empty, BenchmarkMeasure.Capture(() => {
                }).AllocatedBytes);
                array = Math.Min(array, BenchmarkMeasure.Capture(() => sink = new byte[1000]).AllocatedBytes);
            }

            Assert.IsNotNull(sink);
            Assert.AreEqual(0, empty);
            Assert.GreaterOrEqual(array, 1000);
            Assert.Less(array, 1100);
        }

        [Test]
        [Explicit("Opt-in benchmark. Select and Run Selected in Test Runner.")]
        public void GeneratedContainer_SyntheticGraph_RecordsMetrics()
        {
            var report = GeneratedContainerBenchmarkHost.Run();
            Log(report);
            AssertReport(report, GeneratedContainerBenchmarkHost.ContainerName);
        }

        [Test]
        [Explicit("Opt-in allocation breakdown. Select and Run Selected in Test Runner.")]
        // GC.Collect before each of ~370 captures takes longer than the default 180 s.
        [Timeout(600000)]
        public void GeneratedContainer_BuildAllocations_ByStep()
        {
            using (GeneratedContainerBenchmarkHost.RegisterScopes())
            {
                GeneratedContainerBenchmarkHost.OpenSession().Dispose();

                Log("Diagnostics on\n" +
                    GeneratedContainerBenchmarkHost.BuildAllocations(ContainerBenchmarkRunner.Runs));

                using (GeneratedContainerBenchmarkHost.WithoutDiagnostics())
                    Log("Diagnostics off\n" +
                        GeneratedContainerBenchmarkHost.BuildAllocations(ContainerBenchmarkRunner.Runs));
            }
        }

        [Test]
        [Explicit("Opt-in benchmark. Select and Run Selected in Test Runner.")]
        public void GeneratedContainer_ManyScopes_RecordsMetrics()
        {
            using (GeneratedContainerBenchmarkHost.RegisterScopes())
            using (GeneratedContainerBenchmarkHost.WithoutDiagnostics())
            {
                Log(GeneratedContainerBenchmarkHost.ManyScopes(
                    GeneratedContainerBenchmarkHost.ManyScopesCount,
                    ContainerBenchmarkRunner.Runs));
            }
        }

        [Test]
        [Explicit("Opt-in allocation breakdown. Select and Run Selected in Test Runner.")]
        public void GeneratedContainer_EventLoopPhases_RecordsAllocations()
        {
            using (GeneratedContainerBenchmarkHost.RegisterScopes())
            using (GeneratedContainerBenchmarkHost.WithoutDiagnostics())
                Log(GeneratedContainerBenchmarkHost.EventLoopAllocations(ContainerBenchmarkRunner.Runs));
        }

        // BenchmarkRoots duplicates the table with static calls: check that the graph is the same.
        [Test]
        public void GeneratedContainer_Roots_MatchTable()
        {
            using (GeneratedContainerBenchmarkHost.RegisterScopes())
                using (var session = GeneratedContainerBenchmarkHost.OpenSession())
                {
                    foreach (var registration in BenchmarkGraph.Registrations)
                    {
                        var scope = session.Scope(registration.Scope);

                        Assert.IsInstanceOf(
                            registration.Implementation,
                            scope.Resolve(registration.Implementation),
                            $"{registration.Implementation.Name} at {registration.Scope}");
                    }

                    foreach (BenchmarkScopeLevel level in Enum.GetValues(typeof(BenchmarkScopeLevel)))
                    {
                        foreach (var marker in BenchmarkGraph.Markers)
                        {
                            Assert.AreEqual(
                                CountMarker(level, marker),
                                ResolveAllCount(session.Scope(level), marker),
                                $"{marker.Name} at {level}");
                        }
                    }
                }
        }

        private static int CountMarker(BenchmarkScopeLevel level, Type marker)
        {
            var count = 0;

            foreach (var registration in BenchmarkGraph.Registrations)
            {
                if (registration.Scope == level && Array.IndexOf(registration.Markers, marker) >= 0)
                    count++;
            }

            return count;
        }

        private static int ResolveAllCount(IContainer container, Type marker)
        {
            var method = typeof(IContainer).GetMethod(nameof(IContainer.ResolveAll)).MakeGenericMethod(marker);
            return ((System.Collections.ICollection)method.Invoke(container, null)).Count;
        }

        // FrameworkMenu.Container + "Run Benchmark", GroupPriority: the tests do not reference the editor assembly.
        [MenuItem("Tools/Framework/Container/Run Benchmark", priority = 2020)]
        private static void RunFromMenu()
        {
            try
            {
                var generated = GeneratedContainerBenchmarkHost.Run();
                Debug.Log(generated.Format());
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Generated container benchmark threw (do not invent numbers): " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        private static void Log(BenchmarkReport report)
        {
            Log(report.Format());
        }

        private static void Log(string text)
        {
            Debug.Log(text);
            TestContext.WriteLine(text);
        }

        private static void AssertReport(BenchmarkReport report, string containerName)
        {
            Assert.AreEqual(containerName, report.ContainerName);
            Assert.Greater(report.BuildResolved.Ticks, 0);
            Assert.Greater(report.Singleton10k.Ticks, 0);
            Assert.Greater(report.Transient10k.Ticks, 0);
            Assert.Greater(report.Build.AllocatedBytes, 0);
            Assert.Greater(report.BuildResolved.AllocatedBytes, 0);
            Assert.Greater(report.Transient10k.AllocatedBytes, 0);
        }
    }
}