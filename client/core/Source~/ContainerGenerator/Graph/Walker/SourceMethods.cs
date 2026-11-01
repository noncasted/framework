using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class SourceMethods {
        private readonly Compilation _compilation;

        public SourceMethods(Compilation compilation) {
            _compilation = compilation;
        }

        // The IDE gives neighbouring projects as compilations, and their methods have syntax trees of another compilation.
        // Such a method is read as metadata: its registrations come from the manifest of its assembly.
        public SyntaxReference? LocalSyntax(ISymbol symbol) {
            foreach (var reference in symbol.DeclaringSyntaxReferences) {
                if (_compilation.ContainsSyntaxTree(reference.SyntaxTree))
                    return reference;
            }

            return null;
        }
    }
}
