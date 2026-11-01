using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Compiled into a temporary copy of ContainerGenerator.Verify by verify_output.sh, never into the tree. The classes
// below shadow the ones Verify uses by name (a type of the enclosing namespace wins over a using), so the sources of
// Verify stay as they are: every compilation of a case also goes through the whole generator, and every direct call of
// an emitter is recorded.
namespace ContainerGenerator.Verify {
    internal static class CSharpCompilation {
        public static Microsoft.CodeAnalysis.CSharp.CSharpCompilation Create(
            string assemblyName,
            IEnumerable<SyntaxTree> trees,
            IEnumerable<MetadataReference> references,
            CSharpCompilationOptions options) {
            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(assemblyName, trees, references, options);
            VerifyCapture.Driver(compilation);
            return compilation;
        }
    }

    internal static class ScopeEmitter {
        public static bool TryEmit(
            ScopeGraph graph,
            GraphDocument document,
            Compilation compilation,
            ReferenceSymbols references,
            out string hint,
            out string source) {
            var emitted = ContainerGenerator.ScopeEmitter.TryEmit(graph, document, compilation, references, out hint, out source);
            VerifyCapture.Direct("ScopeEmitter", emitted + "\n" + hint + "\n" + source + "\n" + VerifyCapture.Text(graph.Diagnostics));
            return emitted;
        }
    }

    internal static class GraphEmitter {
        public static string Emit(GraphDocument document, Compilation compilation) {
            var source = ContainerGenerator.GraphEmitter.Emit(document, compilation);
            VerifyCapture.Direct("GraphEmitter", source + "\n" + VerifyCapture.Text(document.Diagnostics));
            return source;
        }
    }

    internal static class VerifyCapture {
        private static readonly string Root = Environment.GetEnvironmentVariable("VERIFY_CAPTURE_DIR")
                                              ?? throw new InvalidOperationException("VERIFY_CAPTURE_DIR is not set");
        private static readonly string Tree = Environment.GetEnvironmentVariable("VERIFY_CAPTURE_TREE")
                                              ?? throw new InvalidOperationException("VERIFY_CAPTURE_TREE is not set");
        // A stack trace in a CINGR001 names methods and files of the generator, which a split moves.
        private static readonly Regex StackFrame = new(@"^\s*at .*$\r?\n?", RegexOptions.Multiline | RegexOptions.Compiled);
        private static int _sequence;

        public static void Driver(Compilation compilation) {
            var parse = compilation.SyntaxTrees.First().Options as CSharpParseOptions
                        ?? new CSharpParseOptions(LanguageVersion.Latest);
            var driver = CSharpGeneratorDriver.Create(
                new[] { new ContainerGraphGenerator().AsSourceGenerator() },
                parseOptions: parse);
            var result = driver.RunGenerators(compilation).GetRunResult();
            var folder = Path.Combine(Root, Next() + "_driver_" + compilation.AssemblyName);
            Directory.CreateDirectory(folder);
            foreach (var generated in result.Results.SelectMany(run => run.GeneratedSources))
                File.WriteAllText(Path.Combine(folder, generated.HintName), Clean(generated.SourceText.ToString()));
            File.WriteAllText(Path.Combine(folder, "_diagnostics.txt"), Clean(string.Join("\n", result.Diagnostics.Select(d => d.ToString()))));
            var failures = result.Results.Where(run => run.Exception != null).Select(run => run.Exception!.GetType().FullName);
            File.WriteAllText(Path.Combine(folder, "_exceptions.txt"), string.Join("\n", failures));
        }

        public static void Direct(string kind, string text) {
            File.WriteAllText(Path.Combine(Root, Next() + "_" + kind + ".txt"), Clean(text));
        }

        public static string Text(IEnumerable<DiagnosticInfo> diagnostics) {
            return string.Join("\n", diagnostics.Select(d => d.ToDiagnostic().ToString()));
        }

        private static string Next() {
            return (++_sequence).ToString("D5");
        }

        private static string Clean(string text) {
            return StackFrame.Replace(text, "").Replace(Tree, "<tree>");
        }
    }
}
