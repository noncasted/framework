using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class WalkDiagnostics {
        private readonly GraphDocument _document;

        public WalkDiagnostics(GraphDocument document) {
            _document = document;
        }

        public void Error(WalkState state, SyntaxNode node, string what, DiagnosticDescriptor? descriptor = null) {
            var span = node.GetLocation().GetLineSpan();
            var file = node.SyntaxTree.FilePath ?? "";
            var line = (span.StartLinePosition.Line + 1).ToString();
            _document.Diagnostics.Add(new DiagnosticInfo(
                descriptor ?? GraphDescriptors.UncoveredSyntax,
                LocationInfo.CreateFrom(node.GetLocation()),
                what,
                state.Result.Id,
                file,
                line));
        }

        public void ReportOverloadedRoot(string id, IMethodSymbol method) {
            var location = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
            _document.Diagnostics.Add(new DiagnosticInfo(
                GraphDescriptors.OverloadedRoot,
                LocationInfo.CreateFrom(location),
                id,
                location.SourceTree?.FilePath ?? "",
                (location.GetLineSpan().StartLinePosition.Line + 1).ToString()));
        }
    }
}
