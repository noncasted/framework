using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    // The recursion of the walk: the parts call each other through it, only the composer knows them all.
    internal interface IGraphWalk {
        void WalkStatement(WalkState state, StatementSyntax statement);
        void WalkExpression(WalkState state, ExpressionSyntax expression, bool allowUnknown = false);
        void WalkLambda(WalkState state, ExpressionSyntax expression);
        void WalkReceiver(WalkState state, InvocationExpressionSyntax invocation);
        GraphMethod WalkMethod(IMethodSymbol method, bool isRoot, bool linkToContainer = true);
        GraphMethod WalkVariant(IMethodSymbol method, string id, string variant, string viewType, Dictionary<string, bool> bools);
    }
}
