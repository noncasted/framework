using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Framework.Analyzers
{
    // FWR003: one top-level type per file; X may share it with IX and XExtensions. FWR006: the file is named after its
    // type (any of an allowed group: IX.cs, X.cs and XExtensions.cs all hold the same group).
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TopLevelTypesAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Rules.TopLevelTypes, Rules.FileName);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxTreeAction(Analyze);
        }

        private static void Analyze(SyntaxTreeAnalysisContext context)
        {
            if (ThirdParty.Is(context.Tree, context.Options))
                return;

            var declarations = TopLevelTypes.Of(context.Tree.GetRoot(context.CancellationToken));

            if (declarations.Count == 0)
                return;

            var names = declarations.Select(TopLevelTypes.NameOf).Distinct().ToList();
            var stem = TopLevelTypes.FileStem(context.Tree.FilePath);
            var main = MainName(names, stem);
            var allowed = new HashSet<string> { main, "I" + main, main + "Extensions" };

            foreach (var declaration in declarations)
            {
                var name = TopLevelTypes.NameOf(declaration);

                if (allowed.Contains(name))
                    continue;

                var location = TopLevelTypes.LocationOf(declaration);
                context.ReportDiagnostic(Diagnostic.Create(Rules.TopLevelTypes, location, name, main));
            }

            if (stem.Length == 0 || names.Contains(stem))
                return;

            var mainDeclaration = declarations.First(declaration => TopLevelTypes.NameOf(declaration) == main);
            var fileName = System.IO.Path.GetFileName(context.Tree.FilePath);
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.FileName,
                TopLevelTypes.LocationOf(mainDeclaration),
                fileName,
                main));
        }

        // The type the file is about: the one whose IX / XExtensions companions cover the most names, the type the
        // file is named after first.
        private static string MainName(List<string> names, string stem)
        {
            var best = names[0];
            var bestCovered = -1;

            foreach (var candidate in names)
            {
                var covered = names.Count(name => name == candidate || name == "I" + candidate || name == candidate + "Extensions");
                var isBetter = covered > bestCovered || (covered == bestCovered && candidate == stem);

                if (isBetter)
                {
                    best = candidate;
                    bestCovered = covered;
                }
            }

            return best;
        }
    }
}
