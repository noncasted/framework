using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class UnenumerableConditions {
        private readonly Compilation _compilation;
        private readonly WalkDiagnostics _walkDiagnostics;

        public UnenumerableConditions(Compilation compilation, WalkDiagnostics walkDiagnostics) {
            _compilation = compilation;
            _walkDiagnostics = walkDiagnostics;
        }

        public void ReportUnenumerableVariants(IMethodSymbol construct) {
            foreach (var reference in construct.DeclaringSyntaxReferences) {
                if (_compilation.ContainsSyntaxTree(reference.SyntaxTree) == false)
                    continue;
                var syntax = reference.GetSyntax();
                var model = _compilation.GetSemanticModel(syntax.SyntaxTree);
                var state = new WalkState(construct, model, new GraphMethod { Id = WalkSyntax.MethodId(construct) });
                foreach (var node in syntax.DescendantNodes()) {
                    if (node is not IfStatementSyntax ifs)
                        continue;
                    if (IsNonEnumerableGraphCondition(state, ifs) == false)
                        continue;
                    _walkDiagnostics.Error(state, ifs.Condition, ifs.Condition.ToString(), GraphDescriptors.UnenumerableVariant);
                }
            }
        }

        public bool IsNonEnumerableGraphCondition(WalkState state, IfStatementSyntax syntax) {
            if (HasRegistration(state, syntax.Statement) == false)
                return false;
            if (syntax.Else == null || HasRegistration(state, syntax.Else.Statement) == false)
                return false;
            return IsEnumerableDiscriminant(state, syntax.Condition) == false;
        }

        private bool HasRegistration(WalkState state, StatementSyntax statement) {
            foreach (var node in statement.DescendantNodesAndSelf()) {
                if (node is not InvocationExpressionSyntax invocation)
                    continue;
                var name = WalkSyntax.MethodName(invocation);
                var primitive = Primitives.Of(state.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol, name);
                if (Primitives.IsRegistration(primitive) || name.StartsWith("Add", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private bool IsEnumerableDiscriminant(WalkState state, ExpressionSyntax condition) {
            condition = WalkSyntax.Unwrap(condition);
            if (condition is PrefixUnaryExpressionSyntax not && not.IsKind(SyntaxKind.LogicalNotExpression))
                condition = WalkSyntax.Unwrap(not.Operand);
            if (condition is BinaryExpressionSyntax binary &&
                (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression))) {
                if (VariantConditions.IsTrueLiteral(WalkSyntax.Unwrap(binary.Right)) || VariantConditions.IsFalseLiteral(WalkSyntax.Unwrap(binary.Right)))
                    return IsEnumerableDiscriminant(state, binary.Left);
                if (VariantConditions.IsTrueLiteral(WalkSyntax.Unwrap(binary.Left)) || VariantConditions.IsFalseLiteral(WalkSyntax.Unwrap(binary.Left)))
                    return IsEnumerableDiscriminant(state, binary.Right);
            }

            var type = state.Model.GetTypeInfo(condition).Type;
            if (type == null)
                return false;
            if (type.SpecialType == SpecialType.System_Boolean)
                return IsBoolParameter(state, condition);
            return IsClosedEnum(type);
        }

        private static bool IsBoolParameter(WalkState state, ExpressionSyntax condition) {
            condition = WalkSyntax.Unwrap(condition);
            if (condition is IdentifierNameSyntax identifier) {
                if (state.Locals.ContainsKey(identifier.Identifier.Text))
                    return true;
                var symbol = state.Model.GetSymbolInfo(identifier).Symbol;
                return symbol is IParameterSymbol || symbol is ILocalSymbol;
            }

            if (condition is MemberAccessExpressionSyntax)
                return true;
            return false;
        }

        private static bool IsClosedEnum(ITypeSymbol type) {
            if (type.TypeKind != TypeKind.Enum)
                return false;
            foreach (var attribute in type.GetAttributes()) {
                if (attribute.AttributeClass != null && attribute.AttributeClass.Name == "FlagsAttribute")
                    return false;
            }

            return true;
        }
    }
}
