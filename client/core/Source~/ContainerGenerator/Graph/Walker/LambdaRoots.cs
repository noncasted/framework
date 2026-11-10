using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    // A lambda or an anonymous method where a scope takes its root: Func<IScopeBuilder, UniTask> or
    // Func<IEntityBuilder, UniTask> (ScopeLoadOptions, LoadRoot, the entity loader). The runtime finds the generated class
    // by the name of the root method (GeneratedScopes.RootId) and a lambda has none: it compiles and fails every load.
    internal static class LambdaRoots {
        public static void Check(SemanticModel model, AnonymousFunctionExpressionSyntax lambda, WalkDiagnostics diagnostics) {
            if (model.GetTypeInfo(lambda).ConvertedType is not INamedTypeSymbol type || IsRootDelegate(type) == false)
                return;

            diagnostics.ReportLambdaRoot(lambda, Target(lambda) ?? type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        // What the lambda is given to: the called method or the constructed type, null when it is not an argument.
        public static string? Target(SyntaxNode lambda) {
            var node = lambda.Parent;
            while (node is ParenthesizedExpressionSyntax || node is CastExpressionSyntax)
                node = node.Parent;

            if (node is not ArgumentSyntax { Parent: ArgumentListSyntax { Parent: var call } })
                return null;
            if (call is InvocationExpressionSyntax invocation)
                return WalkSyntax.MethodName(invocation);
            if (call is ObjectCreationExpressionSyntax creation)
                return creation.Type.ToString();
            return null;
        }

        private static bool IsRootDelegate(INamedTypeSymbol type) {
            var invoke = type.DelegateInvokeMethod;
            if (invoke == null || invoke.Parameters.Length != 1 || invoke.ReturnType.Name != "UniTask")
                return false;

            var builder = invoke.Parameters[0].Type.Name;
            return builder == "IScopeBuilder" || builder == "IEntityBuilder";
        }
    }
}
