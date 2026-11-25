using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Framework.Analyzers
{
    // Tells tests and benchmarks from runtime code. A file is a test or a benchmark when its assembly is named so
    // (Foo.Tests, Foo.PlayMode.Tests, Foo.Tests.Fixtures, Foo.TestTools, Benchmarks), when a folder of its path is
    // Tests, Tests~, Test, Benchmarks or ends with .Tests / .Benchmarks, or when the option framework_rules.code_kind
    // of .editorconfig says test or benchmark for it. Generated code is skipped by Roslyn itself (auto-generated header,
    // .g.cs, .generated.cs, .designer.cs).
    public static class CodeKind
    {
        public const string Option = "framework_rules.code_kind";

        private static readonly string[] _testFolders = { "tests", "tests~", "test", "benchmarks" };
        private static readonly string[] _testSuffixes = { ".tests", ".benchmarks" };
        private static readonly string[] _testAssemblySuffixes = { "tests", "benchmarks", ".testtools", ".tests.fixtures", ".verify" };

        public static bool IsTestOrBenchmark(SyntaxTree tree, string? assemblyName, AnalyzerOptions options)
        {
            var config = options.AnalyzerConfigOptionsProvider.GetOptions(tree);

            if (config.TryGetValue(Option, out var kind))
                return kind == "test" || kind == "benchmark";

            if (assemblyName != null && IsTestAssembly(assemblyName))
                return true;

            return IsTestPath(tree.FilePath);
        }

        private static bool IsTestAssembly(string assemblyName)
        {
            var name = assemblyName.ToLowerInvariant();

            foreach (var suffix in _testAssemblySuffixes)
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsTestPath(string path)
        {
            var segments = path.Replace('\\', '/').Split('/');

            // The last segment is the file itself.
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var segment = segments[i].ToLowerInvariant();

                if (Array.IndexOf(_testFolders, segment) >= 0)
                    return true;

                foreach (var suffix in _testSuffixes)
                {
                    if (segment.EndsWith(suffix, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }
    }
}
