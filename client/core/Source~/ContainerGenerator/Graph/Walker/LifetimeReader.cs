using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class LifetimeReader {
        private readonly WalkDiagnostics _walkDiagnostics;

        public LifetimeReader(WalkDiagnostics walkDiagnostics) {
            _walkDiagnostics = walkDiagnostics;
        }

        // The graph is built at compile time: the value of the ServiceLifetime parameter must be a constant. The
        // generated class knows the lifetime, the runtime does not store it, so a variable has no way to act.
        public string ReadLifetime(WalkState state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            if (symbol == null)
                return "Singleton";

            IParameterSymbol? parameter = null;
            foreach (var candidate in symbol.Parameters) {
                if (candidate.Type.Name == "ServiceLifetime")
                    parameter = candidate;
            }

            if (parameter == null)
                return "Singleton";

            var argument = WalkSyntax.FindArgument(invocation, parameter, symbol);
            if (argument == null)
                return LifetimeName(parameter.Type, parameter.HasExplicitDefaultValue ? parameter.ExplicitDefaultValue : null);

            var value = state.Model.GetConstantValue(argument.Expression);
            if (value.HasValue)
                return LifetimeName(parameter.Type, value.Value);

            _walkDiagnostics.Error(state, argument, "the lifetime '" + argument.Expression + "', a ServiceLifetime must be a constant");
            return "Singleton";
        }

        private static string LifetimeName(ITypeSymbol enumType, object? value) {
            foreach (var member in enumType.GetMembers()) {
                if (member is IFieldSymbol field && field.HasConstantValue && Equals(field.ConstantValue, value))
                    return field.Name;
            }

            return "Singleton";
        }
    }
}
