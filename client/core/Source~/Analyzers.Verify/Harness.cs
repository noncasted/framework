using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Framework.Analyzers.Verify
{
    // Compiles the sources of a case with the real analyzers and an .editorconfig, compares the diagnostics of the rules
    // the name of the case starts with ("FWR003+FWR006 ...") with the expected "FWR001@3" (rule at a 1-based line) list.
    public sealed class Harness
    {
        private static readonly ImmutableArray<DiagnosticAnalyzer> _analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new PublicSetterAnalyzer(),
            new FileLengthAnalyzer(),
            new TopLevelTypesAnalyzer(),
            new CommentLanguageAnalyzer(),
            new LifetimeLockAnalyzer());

        private static readonly ImmutableArray<MetadataReference> _platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

        private int _failed;
        private int _passed;

        public void Case(string name, Source[] sources, string[] expected, string editorConfig = "", string assemblyName = "Game",
            MetadataReference[]? references = null)
        {
            try
            {
                var rules = name.Split(' ')[0].Split('+');
                var actual = Analyze(sources, editorConfig, assemblyName, references ?? Array.Empty<MetadataReference>())
                    .Where(value => rules.Contains(value.Split('@')[0]))
                    .ToArray();
                var expectedSorted = expected.OrderBy(value => value, StringComparer.Ordinal).ToArray();

                if (actual.SequenceEqual(expectedSorted) == false)
                    throw new InvalidOperationException(
                        $"expected [{string.Join(", ", expectedSorted)}], got [{string.Join(", ", actual)}]");

                _passed++;
                Console.WriteLine("PASS  " + name);
            }
            catch (Exception exception)
            {
                _failed++;
                Console.WriteLine("FAIL  " + name);
                Console.WriteLine("      " + exception.Message);
            }
        }

        public int Finish()
        {
            Console.WriteLine(_failed == 0 ? $"ALL PASSED {_passed}" : $"FAILED {_failed} of {_failed + _passed}");
            return _failed == 0 ? 0 : 1;
        }

        // A library compiled from the sources, referenced by a case as a foreign assembly.
        public static MetadataReference Library(string assemblyName, string text)
        {
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText(text) },
                _platform,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);

            if (result.Success == false)
                throw new InvalidOperationException("library does not compile: " + string.Join("; ", result.Diagnostics));

            return MetadataReference.CreateFromImage(stream.ToArray());
        }

        private static string[] Analyze(Source[] sources, string editorConfig, string assemblyName, MetadataReference[] references)
        {
            var config = AnalyzerConfig.Parse("root = true\n[*.cs]\n" + editorConfig, "/project/.editorconfig");
            var configSet = AnalyzerConfigSet.Create(ImmutableArray.Create(config));
            var trees = new List<SyntaxTree>();
            var treeOptions = new Dictionary<SyntaxTree, ImmutableDictionary<string, string>>();
            var severities = new Dictionary<string, ImmutableDictionary<string, ReportDiagnostic>>();

            foreach (var source in sources)
            {
                var path = "/project/" + source.Path;
                var options = configSet.GetOptionsForSourcePath(path);
                var tree = CSharpSyntaxTree.ParseText(source.Text, path: path);
                trees.Add(tree);
                treeOptions[tree] = options.AnalyzerOptions;
                severities[path] = options.TreeOptions;
            }

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithSyntaxTreeOptionsProvider(new SeverityProvider(severities));
            var compilation = CSharpCompilation.Create(assemblyName, trees, _platform.AddRange(references), compilationOptions);
            var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();

            if (errors.Length > 0)
                throw new InvalidOperationException("case does not compile: " + string.Join("; ", errors.Select(e => e.ToString())));

            var analyzerOptions = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new OptionsProvider(treeOptions));
            var diagnostics = compilation
                .WithAnalyzers(_analyzers, analyzerOptions)
                .GetAllDiagnosticsAsync()
                .GetAwaiter()
                .GetResult();
            var crashes = diagnostics.Where(diagnostic => diagnostic.Id == "AD0001").ToArray();

            if (crashes.Length > 0)
                throw new InvalidOperationException("analyzer crashed: " + string.Join("; ", crashes.Select(c => c.GetMessage())));

            return diagnostics
                .Where(diagnostic => diagnostic.Id.StartsWith("FWR", StringComparison.Ordinal))
                .Select(diagnostic => $"{diagnostic.Id}@{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
