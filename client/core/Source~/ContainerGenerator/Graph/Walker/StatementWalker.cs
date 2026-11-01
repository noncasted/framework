using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class StatementWalker {
        private readonly LocalAssignments _localAssignments;
        private readonly SwitchRegistrations _switchRegistrations;
        private readonly UnenumerableConditions _unenumerableConditions;
        private readonly IGraphWalk _walk;
        private readonly WalkDiagnostics _walkDiagnostics;

        public StatementWalker(LocalAssignments localAssignments, SwitchRegistrations switchRegistrations, UnenumerableConditions unenumerableConditions, IGraphWalk walk, WalkDiagnostics walkDiagnostics) {
            _localAssignments = localAssignments;
            _switchRegistrations = switchRegistrations;
            _unenumerableConditions = unenumerableConditions;
            _walk = walk;
            _walkDiagnostics = walkDiagnostics;
        }

        public void WalkStatement(WalkState state, StatementSyntax statement) {
            switch (statement.Kind()) {
                case SyntaxKind.Block:
                    foreach (var child in ((BlockSyntax)statement).Statements)
                        WalkStatement(state, child);
                    return;
                case SyntaxKind.LocalDeclarationStatement:
                    WalkLocalDeclaration(state, (LocalDeclarationStatementSyntax)statement);
                    return;
                case SyntaxKind.ExpressionStatement:
                    _walk.WalkExpression(state, ((ExpressionStatementSyntax)statement).Expression);
                    return;
                case SyntaxKind.ReturnStatement:
                    if (((ReturnStatementSyntax)statement).Expression != null)
                        WalkReturn(state, ((ReturnStatementSyntax)statement).Expression!);
                    return;
                case SyntaxKind.IfStatement:
                    WalkIf(state, (IfStatementSyntax)statement);
                    return;
                case SyntaxKind.SwitchStatement:
                    WalkSwitchStatement(state, (SwitchStatementSyntax)statement);
                    return;
                case SyntaxKind.UsingStatement:
                    WalkUsing(state, (UsingStatementSyntax)statement);
                    return;
                case SyntaxKind.TryStatement:
                    WalkTry(state, (TryStatementSyntax)statement);
                    return;
                case SyntaxKind.ThrowStatement:
                    if (((ThrowStatementSyntax)statement).Expression != null)
                        _walk.WalkExpression(state, ((ThrowStatementSyntax)statement).Expression!, allowUnknown: true);
                    return;
                case SyntaxKind.LocalFunctionStatement:
                    return;
                case SyntaxKind.EmptyStatement:
                case SyntaxKind.BreakStatement:
                case SyntaxKind.ContinueStatement:
                case SyntaxKind.CheckedStatement:
                case SyntaxKind.UncheckedStatement:
                    return;
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.DoStatement:
                    WalkLoop(state, statement);
                    return;
                default:
                    if (statement is LabeledStatementSyntax labeled) {
                        WalkStatement(state, labeled.Statement);
                        return;
                    }

                    _walkDiagnostics.Error(state, statement, statement.Kind().ToString());
                    return;
            }
        }

        private void WalkLoop(WalkState state, StatementSyntax statement) {
            StatementSyntax? body = statement switch {
                ForStatementSyntax forStatement => forStatement.Statement,
                ForEachStatementSyntax forEach => forEach.Statement,
                WhileStatementSyntax whileStatement => whileStatement.Statement,
                DoStatementSyntax doStatement => doStatement.Statement,
                _ => null,
            };
            if (body != null)
                WalkStatement(state, body);
        }

        private void WalkIf(WalkState state, IfStatementSyntax syntax) {
            _walk.WalkExpression(state, syntax.Condition, allowUnknown: true);
            if (state.Bools.Count > 0 && VariantConditions.TryEvalCondition(state, syntax.Condition, out var takeThen)) {
                if (takeThen)
                    WalkStatement(state, syntax.Statement);
                else if (syntax.Else != null)
                    WalkStatement(state, syntax.Else.Statement);
                return;
            }

            if (state.EntityRoot && _unenumerableConditions.IsNonEnumerableGraphCondition(state, syntax)) {
                _walkDiagnostics.Error(
                    state,
                    syntax.Condition,
                    syntax.Condition.ToString(),
                    GraphDescriptors.UnenumerableVariant);
            }

            var branch = state.NextBranch++;
            state.Branches.Add(branch + ".0");
            WalkStatement(state, syntax.Statement);
            state.Branches.RemoveAt(state.Branches.Count - 1);
            if (syntax.Else != null) {
                state.Branches.Add(branch + ".1");
                WalkStatement(state, syntax.Else.Statement);
                state.Branches.RemoveAt(state.Branches.Count - 1);
            }
        }

        private void WalkSwitchStatement(WalkState state, SwitchStatementSyntax syntax) {
            _walk.WalkExpression(state, syntax.Expression, allowUnknown: true);
            var branch = state.NextBranch++;
            for (var i = 0; i < syntax.Sections.Count; i++) {
                state.Branches.Add(branch + "." + i);
                foreach (var statement in syntax.Sections[i].Statements)
                    WalkStatement(state, statement);
                state.Branches.RemoveAt(state.Branches.Count - 1);
            }
        }

        private void WalkUsing(WalkState state, UsingStatementSyntax syntax) {
            if (syntax.Declaration != null)
                WalkVariableDeclaration(state, syntax.Declaration);
            if (syntax.Expression != null)
                _walk.WalkExpression(state, syntax.Expression, allowUnknown: true);
            WalkStatement(state, syntax.Statement);
        }

        private void WalkTry(WalkState state, TryStatementSyntax syntax) {
            WalkStatement(state, syntax.Block);
            foreach (var catchClause in syntax.Catches)
                WalkStatement(state, catchClause.Block);
            if (syntax.Finally != null)
                WalkStatement(state, syntax.Finally.Block);
        }

        private void WalkReturn(WalkState state, ExpressionSyntax expression) {
            _walk.WalkExpression(state, expression);
            expression = WalkSyntax.Unwrap(expression);
            if (expression is IdentifierNameSyntax identifier &&
                state.Locals.TryGetValue(identifier.Identifier.Text, out var local) &&
                local.Registration != null)
                state.LastRegistration = local.Registration;
        }

        private void WalkLocalDeclaration(WalkState state, LocalDeclarationStatementSyntax syntax) {
            WalkVariableDeclaration(state, syntax.Declaration);
        }

        private void WalkVariableDeclaration(WalkState state, VariableDeclarationSyntax syntax) {
            foreach (var variable in syntax.Variables) {
                var name = variable.Identifier.Text;
                var local = new WalkLocal { Name = name };
                if (variable.Initializer != null) {
                    var value = WalkSyntax.Unwrap(variable.Initializer.Value);
                    if (value is SwitchExpressionSyntax switchExpression && _switchRegistrations.TryReadRegisterSwitch(state, switchExpression, out var registration)) {
                        local.IsSwitch = true;
                        local.Registration = registration;
                        state.Result.Registrations.Add(registration);
                    }
                    else {
                        _walk.WalkExpression(state, variable.Initializer.Value);
                        _localAssignments.ApplyAssignment(state, local, variable.Initializer.Value);
                    }
                }

                state.Locals[name] = local;
            }
        }
    }
}
