using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class ExpressionWalker {
        private readonly GraphRelevance _graphRelevance;
        private readonly InvocationWalker _invocationWalker;
        private readonly LocalAssignments _localAssignments;
        private readonly SwitchRegistrations _switchRegistrations;
        private readonly IGraphWalk _walk;
        private readonly WalkDiagnostics _walkDiagnostics;

        public ExpressionWalker(GraphRelevance graphRelevance, InvocationWalker invocationWalker, LocalAssignments localAssignments, SwitchRegistrations switchRegistrations, IGraphWalk walk, WalkDiagnostics walkDiagnostics) {
            _graphRelevance = graphRelevance;
            _invocationWalker = invocationWalker;
            _localAssignments = localAssignments;
            _switchRegistrations = switchRegistrations;
            _walk = walk;
            _walkDiagnostics = walkDiagnostics;
        }

        public void WalkExpression(WalkState state, ExpressionSyntax expression, bool allowUnknown = false) {
            expression = WalkSyntax.Unwrap(expression);
            switch (expression.Kind()) {
                case SyntaxKind.InvocationExpression:
                    _invocationWalker.WalkInvocation(state, (InvocationExpressionSyntax)expression);
                    return;
                case SyntaxKind.SimpleAssignmentExpression:
                case SyntaxKind.AddAssignmentExpression:
                    WalkAssignment(state, (AssignmentExpressionSyntax)expression);
                    return;
                case SyntaxKind.SwitchExpression:
                    WalkSwitchExpression(state, (SwitchExpressionSyntax)expression);
                    return;
                case SyntaxKind.ConditionalExpression:
                    var conditional = (ConditionalExpressionSyntax)expression;
                    WalkExpression(state, conditional.Condition, true);
                    var conditionalBranch = state.NextBranch++;
                    state.Branches.Add(conditionalBranch + ".0");
                    WalkExpression(state, conditional.WhenTrue);
                    state.Branches[state.Branches.Count - 1] = conditionalBranch + ".1";
                    WalkExpression(state, conditional.WhenFalse);
                    state.Branches.RemoveAt(state.Branches.Count - 1);
                    return;
                case SyntaxKind.SimpleLambdaExpression:
                case SyntaxKind.ParenthesizedLambdaExpression:
                case SyntaxKind.AnonymousMethodExpression:
                    WalkLambda(state, expression);
                    return;
                case SyntaxKind.AwaitExpression:
                    WalkExpression(state, ((AwaitExpressionSyntax)expression).Expression);
                    return;
                case SyntaxKind.ObjectCreationExpression:
                case SyntaxKind.ImplicitObjectCreationExpression:
                    WalkObjectCreation(state, expression, allowUnknown);
                    return;
                case SyntaxKind.IdentifierName:
                case SyntaxKind.GenericName:
                case SyntaxKind.ThisExpression:
                case SyntaxKind.BaseExpression:
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                case SyntaxKind.NullLiteralExpression:
                case SyntaxKind.CharacterLiteralExpression:
                case SyntaxKind.DefaultLiteralExpression:
                case SyntaxKind.DefaultExpression:
                case SyntaxKind.TypeOfExpression:
                case SyntaxKind.SizeOfExpression:
                case SyntaxKind.PredefinedType:
                    return;
                case SyntaxKind.SimpleMemberAccessExpression:
                    WalkExpression(state, ((MemberAccessExpressionSyntax)expression).Expression, true);
                    return;
                case SyntaxKind.ElementAccessExpression:
                    var access = (ElementAccessExpressionSyntax)expression;
                    WalkExpression(state, access.Expression, true);
                    foreach (var argument in access.ArgumentList.Arguments)
                        WalkExpression(state, argument.Expression, true);
                    return;
                case SyntaxKind.CastExpression:
                    WalkExpression(state, ((CastExpressionSyntax)expression).Expression, true);
                    return;
                case SyntaxKind.AsExpression:
                case SyntaxKind.IsExpression:
                    WalkExpression(state, ((BinaryExpressionSyntax)expression).Left, true);
                    return;
                case SyntaxKind.IsPatternExpression:
                    WalkExpression(state, ((IsPatternExpressionSyntax)expression).Expression, true);
                    return;
                case SyntaxKind.ConditionalAccessExpression:
                    WalkExpression(state, ((ConditionalAccessExpressionSyntax)expression).Expression, true);
                    return;
                case SyntaxKind.SuppressNullableWarningExpression:
                    WalkExpression(state, ((PostfixUnaryExpressionSyntax)expression).Operand, allowUnknown);
                    return;
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.LogicalNotExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                case SyntaxKind.AddExpression:
                case SyntaxKind.SubtractExpression:
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.DivideExpression:
                case SyntaxKind.ModuloExpression:
                case SyntaxKind.BitwiseAndExpression:
                case SyntaxKind.BitwiseOrExpression:
                case SyntaxKind.ExclusiveOrExpression:
                case SyntaxKind.LeftShiftExpression:
                case SyntaxKind.RightShiftExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.CoalesceExpression:
                case SyntaxKind.UnaryMinusExpression:
                case SyntaxKind.UnaryPlusExpression:
                case SyntaxKind.BitwiseNotExpression:
                case SyntaxKind.PreIncrementExpression:
                case SyntaxKind.PreDecrementExpression:
                case SyntaxKind.PostIncrementExpression:
                case SyntaxKind.PostDecrementExpression:
                    foreach (var child in expression.ChildNodes()) {
                        if (child is ExpressionSyntax childExpression)
                            WalkExpression(state, childExpression, true);
                    }

                    return;
                case SyntaxKind.InterpolatedStringExpression:
                case SyntaxKind.TupleExpression:
                case SyntaxKind.AnonymousObjectCreationExpression:
                case SyntaxKind.ImplicitArrayCreationExpression:
                case SyntaxKind.ArrayCreationExpression:
                    foreach (var child in expression.ChildNodes()) {
                        if (child is ExpressionSyntax childExpression)
                            WalkExpression(state, childExpression, true);
                    }

                    return;
                default:
                    if (allowUnknown)
                        return;
                    if (_graphRelevance.IsGraphRelevant(state, expression) == false)
                        return;
                    _walkDiagnostics.Error(state, expression, expression.Kind().ToString());
                    return;
            }
        }

        private void WalkAssignment(WalkState state, AssignmentExpressionSyntax syntax) {
            WalkExpression(state, syntax.Right);
            if (syntax.Left is IdentifierNameSyntax identifier && state.Locals.TryGetValue(identifier.Identifier.Text, out var local))
                _localAssignments.ApplyAssignment(state, local, syntax.Right);
            else if (syntax.Left.ToString() == "_") {
                if (WalkSyntax.Unwrap(syntax.Right) is SwitchExpressionSyntax switchExpression &&
                        _switchRegistrations.TryApplyParameterSwitch(state, switchExpression) == false &&
                        _graphRelevance.IsGraphRelevant(state, switchExpression))
                    _walkDiagnostics.Error(state, switchExpression, "switch expression over installer calls");
            }
        }

        public void WalkLambda(WalkState state, ExpressionSyntax expression) {
            CSharpSyntaxNode? body = expression switch {
                SimpleLambdaExpressionSyntax simple => simple.Body,
                ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Body,
                AnonymousMethodExpressionSyntax anonymous => anonymous.Body,
                _ => null,
            };
            if (body is StatementSyntax statement)
                _walk.WalkStatement(state, statement);
            else if (body is ExpressionSyntax bodyExpression)
                WalkExpression(state, bodyExpression);
        }

        private void WalkObjectCreation(WalkState state, ExpressionSyntax expression, bool allowUnknown) {
            ArgumentListSyntax? arguments = expression switch {
                ObjectCreationExpressionSyntax creation => creation.ArgumentList,
                ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.ArgumentList,
                _ => null,
            };
            if (arguments == null)
                return;

            foreach (var argument in arguments.Arguments) {
                var value = WalkSyntax.Unwrap(argument.Expression);
                if (value is SimpleLambdaExpressionSyntax || value is ParenthesizedLambdaExpressionSyntax || value is AnonymousMethodExpressionSyntax)
                    WalkLambda(state, value);
                else
                    WalkExpression(state, argument.Expression, allowUnknown: true);
            }
        }

        private void WalkSwitchExpression(WalkState state, SwitchExpressionSyntax syntax) {
            if (_switchRegistrations.TryReadRegisterSwitch(state, syntax, out var registration)) {
                state.Result.Registrations.Add(registration);
                return;
            }

            if (_switchRegistrations.TryApplyParameterSwitch(state, syntax))
                return;

            WalkExpression(state, syntax.GoverningExpression, true);
            var branch = state.NextBranch++;
            for (var i = 0; i < syntax.Arms.Count; i++) {
                state.Branches.Add(branch + "." + i);
                WalkExpression(state, syntax.Arms[i].Expression);
                state.Branches.RemoveAt(state.Branches.Count - 1);
            }
        }

        public void WalkReceiver(WalkState state, InvocationExpressionSyntax invocation) {
            var receiver = WalkSyntax.GetReceiver(invocation);
            if (receiver != null)
                WalkExpression(state, receiver, true);
        }
    }
}
