using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    // The harvested copy of an installer lives in another class: what the original reached through its own class does not
    // bind there. An unqualified call of a static method (Helper(builder)) gets the type of the method, an extension of the
    // class of the installer (builder.Configure(), private as well) is called in its static form. A private callee stays
    // inaccessible in the copy; the walker takes its single inaccessible candidate.
    internal sealed class HarvestQualifier : CSharpSyntaxRewriter {
        private readonly SemanticModel _model;
        private readonly INamedTypeSymbol _type;

        public HarvestQualifier(SemanticModel model, INamedTypeSymbol type) {
            _model = model;
            _type = type;
        }

        public string Text(SyntaxNode node) {
            return Visit(node).ToString();
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
            var symbol = _model.GetSymbolInfo(node).Symbol as IMethodSymbol;
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (symbol == null)
                return visited;

            if (IsUnqualifiedStatic(node, symbol)) {
                var qualified = SyntaxFactory.ParseExpression(TypeNames.ForCode(symbol.ContainingType) + "." + visited.Expression.ToString());
                return visited.WithExpression(qualified);
            }

            if (IsOwnExtension(symbol) && visited.Expression is MemberAccessExpressionSyntax access) {
                var definition = symbol.ReducedFrom!;
                var callee = SyntaxFactory.ParseExpression(TypeNames.ForCode(definition.ContainingType) + "." + access.Name.ToString());
                var receiver = SyntaxFactory.Argument(access.Expression.WithoutTrivia());
                var arguments = visited.ArgumentList.WithArguments(visited.ArgumentList.Arguments.Insert(0, receiver));
                return SyntaxFactory.InvocationExpression(callee, arguments);
            }

            return visited;
        }

        private static bool IsUnqualifiedStatic(InvocationExpressionSyntax node, IMethodSymbol symbol) {
            if (node.Expression is not IdentifierNameSyntax && node.Expression is not GenericNameSyntax)
                return false;

            return symbol.MethodKind == MethodKind.Ordinary && symbol.IsStatic && symbol.ContainingType != null;
        }

        private bool IsOwnExtension(IMethodSymbol symbol) {
            if (symbol.MethodKind != MethodKind.ReducedExtension || symbol.ReducedFrom == null)
                return false;

            for (var type = _type; type != null; type = type.ContainingType) {
                if (SymbolEqualityComparer.Default.Equals(symbol.ReducedFrom.ContainingType, type))
                    return true;
            }

            return false;
        }
    }
}
