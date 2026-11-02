using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class LocalAssignments {
        private readonly InstallerClassifier _installerClassifier;

        public LocalAssignments(InstallerClassifier installerClassifier) {
            _installerClassifier = installerClassifier;
        }

        public void ApplyAssignment(WalkState state, WalkLocal local, ExpressionSyntax value) {
            value = WalkSyntax.Unwrap(value);
            local.SetType(state.Model.GetTypeInfo(value).Type);
            if (value is InvocationExpressionSyntax invocation) {
                var primitive = Primitives.Of(state.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol, WalkSyntax.MethodName(invocation));
                if (primitive == PrimitiveKind.Instantiate) {
                    local.MarkInstantiated();
                    return;
                }

                if (Primitives.IsRegistration(primitive) || primitive == PrimitiveKind.As || primitive == PrimitiveKind.WithParameter)
                    local.SetRegistration(state.LastRegistration);
                if (state.LastCallIndex >= 0)
                    local.SetReturnedCall(state.LastCallIndex);
                if (_installerClassifier.IsBuilderLike(local.Type))
                    local.MarkBuilder();
            }

            if (local.IsBuilder)
                state.TaintedBuilder.Add(local.Name);
        }
    }
}
