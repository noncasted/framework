using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Framework.Analyzers
{
    // FWR004: a comment (//, /* */, /// and /** */) with a letter of the Cyrillic blocks. String literals are not comments.
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CommentLanguageAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Rules.CommentLanguage);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxTreeAction(Analyze);
        }

        private static void Analyze(SyntaxTreeAnalysisContext context)
        {
            var root = context.Tree.GetRoot(context.CancellationToken);

            foreach (var trivia in root.DescendantTrivia())
            {
                if (IsComment(trivia) == false)
                    continue;

                if (HasCyrillic(trivia.ToFullString()) == false)
                    continue;

                context.ReportDiagnostic(Diagnostic.Create(Rules.CommentLanguage, trivia.GetLocation()));
            }
        }

        private static bool IsComment(SyntaxTrivia trivia)
        {
            switch (trivia.Kind())
            {
                case SyntaxKind.SingleLineCommentTrivia:
                case SyntaxKind.MultiLineCommentTrivia:
                case SyntaxKind.SingleLineDocumentationCommentTrivia:
                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                    return true;
                default:
                    return false;
            }
        }

        // Cyrillic (U+0400-U+04FF) and Cyrillic Supplement (U+0500-U+052F).
        private static bool HasCyrillic(string text)
        {
            foreach (var symbol in text)
            {
                if (symbol >= 'Ѐ' && symbol <= 'ԯ')
                    return true;
            }

            return false;
        }
    }
}
