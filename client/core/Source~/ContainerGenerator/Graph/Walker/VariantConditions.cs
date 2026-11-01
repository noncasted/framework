using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal static class VariantConditions {
        public static void CollectAssignments(InvocationExpressionSyntax load, Dictionary<string, bool> into) {
            foreach (var ancestor in load.Ancestors()) {
                if (ancestor is not IfStatementSyntax ifs)
                    continue;
                var inThen = load.Span.Start >= ifs.Statement.Span.Start &&
                             load.Span.End <= ifs.Statement.Span.End;
                ApplyCondition(ifs.Condition, inThen, into);
            }
        }

        private static void ApplyCondition(ExpressionSyntax condition, bool inThen, Dictionary<string, bool> into) {
            condition = WalkSyntax.Unwrap(condition);
            var polarity = inThen;
            if (condition is PrefixUnaryExpressionSyntax not && not.IsKind(SyntaxKind.LogicalNotExpression)) {
                polarity = !polarity;
                condition = WalkSyntax.Unwrap(not.Operand);
            }

            if (condition is BinaryExpressionSyntax binary &&
                (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression))) {
                var left = WalkSyntax.Unwrap(binary.Left);
                var right = WalkSyntax.Unwrap(binary.Right);
                if (IsTrueLiteral(right) || IsFalseLiteral(right)) {
                    var equalsTrue = IsTrueLiteral(right);
                    if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                        equalsTrue = !equalsTrue;
                    StoreBool(left, polarity == equalsTrue, into);
                    return;
                }

                if (IsTrueLiteral(left) || IsFalseLiteral(left)) {
                    var equalsTrue = IsTrueLiteral(left);
                    if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                        equalsTrue = !equalsTrue;
                    StoreBool(right, polarity == equalsTrue, into);
                    return;
                }
            }

            StoreBool(condition, polarity, into);
        }

        private static void StoreBool(ExpressionSyntax expression, bool value, Dictionary<string, bool> into) {
            expression = WalkSyntax.Unwrap(expression);
            var text = expression.ToString();
            if (string.IsNullOrEmpty(text) == false)
                into[text] = value;
            if (expression is IdentifierNameSyntax identifier)
                into[identifier.Identifier.Text] = value;
            if (expression is MemberAccessExpressionSyntax member)
                into[member.Name.Identifier.Text] = value;
        }

        public static bool TryEvalCondition(WalkState state, ExpressionSyntax condition, out bool value) {
            value = false;
            condition = WalkSyntax.Unwrap(condition);
            if (condition is PrefixUnaryExpressionSyntax not && not.IsKind(SyntaxKind.LogicalNotExpression)) {
                if (TryEvalCondition(state, not.Operand, out var inner) == false)
                    return false;
                value = !inner;
                return true;
            }

            if (condition is BinaryExpressionSyntax binary &&
                (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression))) {
                var left = WalkSyntax.Unwrap(binary.Left);
                var right = WalkSyntax.Unwrap(binary.Right);
                if ((IsTrueLiteral(right) || IsFalseLiteral(right)) && TryLookupBool(state, left, out value)) {
                    var equalsTrue = IsTrueLiteral(right);
                    if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                        equalsTrue = !equalsTrue;
                    if (equalsTrue == false)
                        value = !value;
                    return true;
                }

                if ((IsTrueLiteral(left) || IsFalseLiteral(left)) && TryLookupBool(state, right, out value)) {
                    var equalsTrue = IsTrueLiteral(left);
                    if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                        equalsTrue = !equalsTrue;
                    if (equalsTrue == false)
                        value = !value;
                    return true;
                }
            }

            return TryLookupBool(state, condition, out value);
        }

        private static bool TryLookupBool(WalkState state, ExpressionSyntax expression, out bool value) {
            value = false;
            expression = WalkSyntax.Unwrap(expression);
            var text = expression.ToString();
            if (state.Bools.TryGetValue(text, out value))
                return true;
            if (expression is IdentifierNameSyntax identifier && state.Bools.TryGetValue(identifier.Identifier.Text, out value))
                return true;
            if (expression is MemberAccessExpressionSyntax member &&
                state.Bools.TryGetValue(member.Name.Identifier.Text, out value))
                return true;
            return false;
        }

        public static bool IsTrueLiteral(ExpressionSyntax expression) {
            return expression.IsKind(SyntaxKind.TrueLiteralExpression);
        }

        public static bool IsFalseLiteral(ExpressionSyntax expression) {
            return expression.IsKind(SyntaxKind.FalseLiteralExpression);
        }
    }
}
