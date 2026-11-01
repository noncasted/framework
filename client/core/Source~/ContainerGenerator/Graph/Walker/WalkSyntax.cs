using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal static class WalkSyntax {
        public static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
            while (true) {
                if (expression is ParenthesizedExpressionSyntax parenthesized)
                    expression = parenthesized.Expression;
                else if (expression is PostfixUnaryExpressionSyntax postfix && postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                    expression = postfix.Operand;
                else
                    return expression;
            }
        }

        public static ExpressionSyntax? GetReceiver(InvocationExpressionSyntax invocation) {
            if (invocation.Expression is MemberAccessExpressionSyntax member)
                return member.Expression;
            if (invocation.Expression is MemberBindingExpressionSyntax &&
                invocation.Parent is ConditionalAccessExpressionSyntax conditional)
                return conditional.Expression;
            return null;
        }

        public static string MethodName(InvocationExpressionSyntax invocation) {
            if (invocation.Expression is MemberAccessExpressionSyntax member)
                return member.Name.Identifier.ValueText;
            if (invocation.Expression is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;
            if (invocation.Expression is GenericNameSyntax generic)
                return generic.Identifier.ValueText;
            if (invocation.Expression is MemberBindingExpressionSyntax binding)
                return binding.Name.Identifier.ValueText;
            return invocation.Expression.ToString();
        }

        public static string MethodId(IMethodSymbol method) {
            return MethodIds.Of(method);
        }

        public static string Trim(string value) {
            var compact = value.Replace("\r", " ").Replace("\n", " ");
            while (compact.IndexOf("  ", StringComparison.Ordinal) >= 0)
                compact = compact.Replace("  ", " ");
            if (compact.Length > 240)
                compact = compact.Substring(0, 240) + "...";
            return compact.Trim();
        }

        public static ArgumentSyntax? FindArgument(InvocationExpressionSyntax invocation, IParameterSymbol parameter, IMethodSymbol symbol) {
            var arguments = invocation.ArgumentList.Arguments;
            for (var i = 0; i < arguments.Count; i++) {
                if (arguments[i].NameColon != null && arguments[i].NameColon!.Name.Identifier.ValueText == parameter.Name)
                    return arguments[i];
            }

            var position = symbol.Parameters.IndexOf(parameter);
            if (position < arguments.Count && arguments[position].NameColon == null)
                return arguments[position];
            return null;
        }
    }
}
