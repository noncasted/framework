using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class SwitchRegistrations {
        private readonly LifetimeReader _lifetimeReader;
        private readonly WalkDiagnostics _walkDiagnostics;

        public SwitchRegistrations(LifetimeReader lifetimeReader, WalkDiagnostics walkDiagnostics) {
            _lifetimeReader = lifetimeReader;
            _walkDiagnostics = walkDiagnostics;
        }

        public bool TryReadRegisterSwitch(WalkState state, SwitchExpressionSyntax syntax, out GraphRegistration registration) {
            registration = RegistrationFactory.NewRegistration(state, syntax, "SwitchFactory", "", "SwitchFactory");
            var any = false;
            foreach (var arm in syntax.Arms) {
                var expression = WalkSyntax.Unwrap(arm.Expression);
                if (expression is ThrowExpressionSyntax)
                    continue;
                if (expression is not InvocationExpressionSyntax invocation)
                    return false;

                var symbol = state.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (symbol == null || Primitives.OfSymbol(symbol) != PrimitiveKind.Register || symbol.TypeArguments.Length == 0)
                    return false;

                var implementation = RegistrationFactory.Format(state, symbol.TypeArguments[symbol.TypeArguments.Length == 1 ? 0 : 1]);
                registration.Arms.Add(new GraphSwitchArm {
                    Discriminant = QualifiedPattern(state, arm.Pattern),
                    ImplementationType = implementation,
                });
                any = true;
            }

            if (any == false)
                return false;

            // A switch is one field built once: a transient arm would silently become a singleton.
            foreach (var arm in syntax.Arms) {
                if (WalkSyntax.Unwrap(arm.Expression) is not InvocationExpressionSyntax invocation)
                    continue;
                var symbol = state.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (_lifetimeReader.ReadLifetime(state, invocation, symbol) == "Transient")
                    _walkDiagnostics.Error(state, invocation, "a transient Register in a switch arm, a switch arm is built once");
            }

            state.LastRegistration = registration;
            return true;
        }

        public bool TryApplyParameterSwitch(WalkState state, SwitchExpressionSyntax syntax) {
            var applied = false;
            foreach (var arm in syntax.Arms) {
                var expression = WalkSyntax.Unwrap(arm.Expression);
                if (expression is ThrowExpressionSyntax)
                    continue;
                if (expression is not InvocationExpressionSyntax invocation)
                    return false;

                var symbol = state.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (symbol == null || Primitives.OfSymbol(symbol) != PrimitiveKind.WithParameter)
                    return false;

                var target = RegistrationFactory.CurrentRegistration(state, invocation);
                if (target == null || target.Origin != "SwitchFactory" && target.Arms.Count == 0)
                    return false;

                var argument = invocation.ArgumentList.Arguments.Count > 0
                    ? invocation.ArgumentList.Arguments[0].Expression.ToString()
                    : "";
                var argumentType = invocation.ArgumentList.Arguments.Count > 0
                    ? RegistrationFactory.Format(state, state.Model.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression).Type)
                    : "";
                var discriminant = QualifiedPattern(state, arm.Pattern);
                foreach (var existing in target.Arms) {
                    if (existing.Discriminant == discriminant)
                        {
                        existing.ParameterExpression = argument;
                        existing.ParameterType = argumentType;
                    }
                }

                applied = true;
            }

            return applied;
        }

        // A switch arm over an enum is written with the full name of the constant: the type of the discriminant comes from it.
        private static string QualifiedPattern(WalkState state, PatternSyntax pattern) {
            if (pattern is ConstantPatternSyntax constant &&
                state.Model.GetSymbolInfo(constant.Expression).Symbol is IFieldSymbol field &&
                field.ContainingType != null && field.ContainingType.TypeKind == TypeKind.Enum)
                return TypeNames.ForCode(field.ContainingType) + "." + field.Name;
            return pattern.ToString();
        }
    }
}
