using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    // The text of a harvest copy: the usings of the original, its parameters and its body with the local functions inlined.
    internal static class HarvestText {
        // The copy of the body binds in the same environment as the original: the usings of the file and of the
        // enclosing namespaces. Inside a namespace block they are written with global:: to not catch nested names.
        public static void AppendUsings(MethodDeclarationSyntax method, StringBuilder block) {
            var directives = new List<UsingDirectiveSyntax>();
            if (method.SyntaxTree.GetRoot() is CompilationUnitSyntax unit)
                directives.AddRange(unit.Usings);
            foreach (var ancestor in method.Ancestors()) {
                if (ancestor is NamespaceDeclarationSyntax declaration)
                    directives.AddRange(declaration.Usings);
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var directive in directives) {
                var name = directive.Name.ToString();
                if (name.StartsWith("global::", StringComparison.Ordinal) == false)
                    name = "global::" + name;

                var text = "    using " +
                           (directive.StaticKeyword.IsKind(SyntaxKind.None) ? "" : "static ") +
                           (directive.Alias == null ? "" : directive.Alias.Name.ToString() + " = ") +
                           name + ";";
                if (seen.Add(text))
                    block.AppendLine(text);
            }
        }

        public static string Parameters(IMethodSymbol method) {
            var parts = new List<string>();
            for (var i = 0; i < method.Parameters.Length; i++) {
                var parameter = method.Parameters[i];
                parts.Add(TypeNames.ForCode(parameter.Type) + " " + ScopeNames.Escape(parameter.Name));
            }

            return string.Join(", ", parts);
        }

        public static void Inline(
            MethodDeclarationSyntax method,
            HarvestQualifier qualifier,
            StringBuilder builder,
            List<HarvestSegment> segments) {
            if (method.Body == null) {
                if (method.ExpressionBody != null)
                    WriteStatement(method.ExpressionBody.Expression, ";", qualifier, builder, segments);
                return;
            }

            var locals = new Dictionary<string, LocalFunctionStatementSyntax>(StringComparer.Ordinal);
            foreach (var local in method.Body.DescendantNodes()) {
                if (local is LocalFunctionStatementSyntax function)
                    locals[function.Identifier.Text] = function;
            }

            var writer = new StatementsWriter(locals, qualifier, builder, segments);
            writer.Write(method.Body.Statements);
        }

        private static void WriteStatement(
            SyntaxNode node,
            string suffix,
            HarvestQualifier qualifier,
            StringBuilder builder,
            List<HarvestSegment> segments) {
            const string indent = "            ";
            var text = qualifier.Text(node);
            var start = builder.Length + indent.Length;
            builder.AppendLine(indent + text + suffix);
            segments.Add(new HarvestSegment(start, text.Length, node, text == node.ToString()));
        }

        private sealed class StatementsWriter {
            private readonly Dictionary<string, LocalFunctionStatementSyntax> _locals;
            private readonly HarvestQualifier _qualifier;
            private readonly StringBuilder _builder;
            private readonly List<HarvestSegment> _segments;
            private readonly HashSet<string> _visiting = new HashSet<string>(StringComparer.Ordinal);

            public StatementsWriter(
                Dictionary<string, LocalFunctionStatementSyntax> locals,
                HarvestQualifier qualifier,
                StringBuilder builder,
                List<HarvestSegment> segments) {
                _locals = locals;
                _qualifier = qualifier;
                _builder = builder;
                _segments = segments;
            }

            public void Write(SyntaxList<StatementSyntax> statements) {
                foreach (var statement in statements) {
                    if (statement is LocalFunctionStatementSyntax)
                        continue;

                    if (TryLocalInvocation(statement, _locals, out var name)) {
                        if (_visiting.Add(name) == false)
                            continue;
                        var local = _locals[name];
                        if (local.Body != null)
                            Write(local.Body.Statements);
                        else if (local.ExpressionBody != null)
                            WriteStatement(local.ExpressionBody.Expression, ";", _qualifier, _builder, _segments);
                        _visiting.Remove(name);
                        continue;
                    }

                    WriteStatement(statement, "", _qualifier, _builder, _segments);
                }
            }
        }

        private static bool TryLocalInvocation(
            StatementSyntax statement,
            Dictionary<string, LocalFunctionStatementSyntax> locals,
            out string name) {
            name = "";
            if (statement is not ExpressionStatementSyntax expression)
                return false;
            if (expression.Expression is not InvocationExpressionSyntax invocation)
                return false;
            if (invocation.Expression is not IdentifierNameSyntax identifier)
                return false;
            name = identifier.Identifier.Text;
            return locals.ContainsKey(name);
        }
    }
}
