using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class GraphRelevance {
        private readonly InstallerClassifier _installerClassifier;

        public GraphRelevance(InstallerClassifier installerClassifier) {
            _installerClassifier = installerClassifier;
        }

        public bool ReceiverIsGraphRelevant(WalkState state, InvocationExpressionSyntax invocation) {
            var receiver = WalkSyntax.GetReceiver(invocation);
            if (receiver == null)
                return false;
            return IsGraphRelevant(state, receiver);
        }

        public bool IsGraphRelevant(WalkState state, ExpressionSyntax expression) {
            expression = WalkSyntax.Unwrap(expression);
            if (expression is IdentifierNameSyntax identifier) {
                if (state.TaintedBuilder.Contains(identifier.Identifier.Text))
                    return true;
                if (state.Locals.TryGetValue(identifier.Identifier.Text, out var local) && (local.IsBuilder || local.Registration != null))
                    return true;
            }

            var type = state.Model.GetTypeInfo(expression).Type;
            return _installerClassifier.IsBuilderLike(type) || _installerClassifier.IsRegistrationLike(type);
        }
    }
}
