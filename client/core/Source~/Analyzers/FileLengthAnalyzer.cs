using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Framework.Analyzers
{
    // FWR002: a file of runtime code or of an editor tool above the limit (300 lines, framework_rules.max_file_lines of
    // .editorconfig). Lines are counted as wc -l does: a final line break does not open a line.
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FileLengthAnalyzer : DiagnosticAnalyzer
    {
        public const string MaxLinesOption = "framework_rules.max_file_lines";
        public const int DefaultMaxLines = 300;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Rules.FileLength);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var assemblyName = start.Compilation.AssemblyName;
                start.RegisterSyntaxTreeAction(tree => Analyze(tree, assemblyName));
            });
        }

        private static void Analyze(SyntaxTreeAnalysisContext context, string? assemblyName)
        {
            var tree = context.Tree;

            if (CodeKind.IsTestOrBenchmark(tree, assemblyName, context.Options))
                return;

            var maxLines = MaxLines(context);
            var text = tree.GetText(context.CancellationToken);
            var lines = text.Lines.Count;

            if (lines > 0 && text.Lines[lines - 1].Span.IsEmpty)
                lines--;

            if (lines <= maxLines)
                return;

            var firstLine = text.Lines[0].Span;
            var location = Location.Create(tree, new TextSpan(firstLine.Start, firstLine.Length));
            context.ReportDiagnostic(Diagnostic.Create(Rules.FileLength, location, lines, maxLines));
        }

        private static int MaxLines(SyntaxTreeAnalysisContext context)
        {
            var config = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);

            if (config.TryGetValue(MaxLinesOption, out var value) == false)
                return DefaultMaxLines;

            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) == false)
                throw new System.InvalidOperationException($"{MaxLinesOption} = '{value}' is not a number of lines");

            return parsed;
        }
    }
}
