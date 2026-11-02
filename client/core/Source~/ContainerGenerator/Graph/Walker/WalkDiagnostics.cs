using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class WalkDiagnostics {
        private readonly GraphDocument _document;
        private readonly HashSet<string> _genericRoots = new HashSet<string>(StringComparer.Ordinal);

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
            ReportRoot(GraphDescriptors.OverloadedRoot, id, method);
        }

        // One error per method: a method is seen by its name and by every call of the entity loader.
        public void ReportGenericRoot(IMethodSymbol method) {
            var definition = method.MethodKind == MethodKind.LocalFunction ? method : InstallerClassifier.Normalize(method);
            var id = MethodIds.Of(definition);
            if (_genericRoots.Add(id))
                ReportRoot(GraphDescriptors.GenericRoot, id, method);
        }

        private void ReportRoot(DiagnosticDescriptor descriptor, string id, IMethodSymbol method) {
            var location = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
            _document.Diagnostics.Add(new DiagnosticInfo(
                descriptor,
                LocationInfo.CreateFrom(location),
                id,
                location.SourceTree?.FilePath ?? "",
                (location.GetLineSpan().StartLinePosition.Line + 1).ToString()));
        }
    }
}
