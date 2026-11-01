using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    // The composer of the walk: reads the installer methods of the compilation into one document. The parts recurse into
    // each other (a lambda holds statements, a call walks the called method) through IGraphWalk, which it implements.
    internal sealed class GraphWalker : IGraphWalk {
        private readonly Compilation _compilation;
        private readonly GraphDocument _document;
        private readonly Dictionary<string, GraphMethod> _methods;
        private readonly Dictionary<string, IMethodSymbol> _symbols = new Dictionary<string, IMethodSymbol>(StringComparer.Ordinal);
        private readonly InstallerClassifier _installerClassifier;
        private readonly WalkDiagnostics _walkDiagnostics;
        private readonly SourceMethods _sourceMethods;
        private readonly StatementWalker _statementWalker;
        private readonly ExpressionWalker _expressionWalker;
        private readonly EntityVariants _entityVariants;

        public GraphWalker(Compilation compilation, ReferenceSymbols references) {
            _compilation = compilation;
            _document = new GraphDocument { AssemblyName = compilation.AssemblyName ?? "Assembly" };
            _methods = new Dictionary<string, GraphMethod>(StringComparer.Ordinal);
            _installerClassifier = new InstallerClassifier(references);
            _walkDiagnostics = new WalkDiagnostics(_document);
            _sourceMethods = new SourceMethods(compilation);
            var relevance = new GraphRelevance(_installerClassifier);
            var assignments = new LocalAssignments(_installerClassifier);
            var lifetimes = new LifetimeReader(_walkDiagnostics);
            var switches = new SwitchRegistrations(lifetimes, _walkDiagnostics);
            var unenumerable = new UnenumerableConditions(compilation, _walkDiagnostics);
            var handlers = new RegistrationHandlers(lifetimes, this);
            var invocations = new InvocationWalker(relevance, _installerClassifier, handlers, _sourceMethods, this, _walkDiagnostics);
            _expressionWalker = new ExpressionWalker(relevance, invocations, assignments, switches, this, _walkDiagnostics);
            _statementWalker = new StatementWalker(assignments, switches, unenumerable, this, _walkDiagnostics);
            _entityVariants = new EntityVariants(compilation, unenumerable, this, _walkDiagnostics);
        }

        public void WalkStatement(WalkState state, StatementSyntax statement) {
            _statementWalker.WalkStatement(state, statement);
        }

        public void WalkExpression(WalkState state, ExpressionSyntax expression, bool allowUnknown = false) {
            _expressionWalker.WalkExpression(state, expression, allowUnknown);
        }

        public void WalkLambda(WalkState state, ExpressionSyntax expression) {
            _expressionWalker.WalkLambda(state, expression);
        }

        public void WalkReceiver(WalkState state, InvocationExpressionSyntax invocation) {
            _expressionWalker.WalkReceiver(state, invocation);
        }
        public GraphDocument Walk() {
            foreach (var tree in _compilation.SyntaxTrees) {
                var model = _compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();
                foreach (var node in root.DescendantNodes()) {
                    IMethodSymbol? method = null;
                    if (node is MethodDeclarationSyntax methodSyntax)
                        method = model.GetDeclaredSymbol(methodSyntax) as IMethodSymbol;
                    else if (node is LocalFunctionStatementSyntax localSyntax)
                        method = model.GetDeclaredSymbol(localSyntax) as IMethodSymbol;

                    if (method == null)
                        continue;
                    if (SymbolEqualityComparer.Default.Equals(method.ContainingAssembly, _compilation.Assembly) == false)
                        continue;

                    var isRoot = _installerClassifier.IsRoot(method);
                    if (isRoot == false && _installerClassifier.ShouldWalkAsInstaller(method) == false)
                        continue;

                    WalkMethod(method, isRoot);
                }
            }

            _entityVariants.AttachEntityScopes();

            foreach (var pair in _methods)
                _document.Methods.Add(pair.Value);

            _document.Methods.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return _document;
        }

        // linkToContainer: false for a call of a local function, the call itself keeps its place in the caller.
        public GraphMethod WalkMethod(IMethodSymbol method, bool isRoot, bool linkToContainer = true) {
            if (method.MethodKind != MethodKind.LocalFunction)
                method = InstallerClassifier.Normalize(method);
            var id = MethodIds.Of(method);
            if (_methods.TryGetValue(id, out var existing)) {
                ReportIfOverloaded(id, method);
                if (isRoot)
                    existing.IsRoot = true;
                return existing;
            }

            _symbols[id] = method;

            var result = new GraphMethod {
                Id = id,
                OriginId = id,
                IsRoot = isRoot,
                AssemblyName = _document.AssemblyName,
            };
            _methods[id] = result;
            FillMethod(result, method, null, false);
            if (linkToContainer)
                LinkContainingMethod(method, result);
            return result;
        }

        // Only roots share an id over different methods (MethodIds).
        private void ReportIfOverloaded(string id, IMethodSymbol method) {
            if (_symbols.TryGetValue(id, out var first) == false || SymbolEqualityComparer.Default.Equals(first, method))
                return;

            _walkDiagnostics.ReportOverloadedRoot(id, method);
        }

        private void LinkContainingMethod(IMethodSymbol method, GraphMethod result) {
            if (method.MethodKind != MethodKind.LocalFunction)
                return;
            if (method.ContainingSymbol is not IMethodSymbol parent)
                return;

            var parentId = MethodIds.Of(parent);
            if (_methods.TryGetValue(parentId, out var parentMethod) == false) {
                parentMethod = new GraphMethod {
                    Id = parentId,
                    OriginId = parentId,
                    IsRoot = false,
                    AssemblyName = _document.AssemblyName,
                };
                _methods[parentId] = parentMethod;
            }

            if (parentMethod.Calls.Contains(result.Id))
                return;

            parentMethod.AddCall(result.Id, 0);
        }

        public GraphMethod WalkVariant(
            IMethodSymbol method,
            string id,
            string variant,
            string viewType,
            Dictionary<string, bool> bools) {
            if (_methods.TryGetValue(id, out var existing))
                return existing;

            var origin = MethodIds.Of(method);
            var result = new GraphMethod {
                Id = id,
                OriginId = origin,
                IsRoot = true,
                Variant = variant,
                ViewType = viewType,
                AssemblyName = _document.AssemblyName,
            };
            _methods[id] = result;
            FillMethod(result, method, bools, true);
            if (string.IsNullOrEmpty(result.ParentHint) && _methods.TryGetValue(origin, out var originMethod)) {
                result.ParentHint = originMethod.ParentHint;
                result.ParentAssembly = originMethod.ParentAssembly;
            }

            return result;
        }

        private void FillMethod(
            GraphMethod result,
            IMethodSymbol method,
            Dictionary<string, bool>? bools,
            bool entityRoot) {
            var original = method.OriginalDefinition ?? method;
            var syntaxSource = method.ReducedFrom ?? original;
            SyntaxNode? syntax = null;
            SemanticModel? model = null;
            var reference = _sourceMethods.LocalSyntax(syntaxSource);
            if (reference != null) {
                syntax = reference.GetSyntax();
                model = _compilation.GetSemanticModel(syntax.SyntaxTree);
            }

            // A method of a referenced assembly: its body comes from the manifest of that assembly (ManifestReader),
            // so it belongs there and is not exported again with the manifest of this one.
            if (syntax == null || model == null) {
                result.File = "";
                result.Line = 0;
                if (reference == null && syntaxSource.ContainingAssembly != null)
                    result.AssemblyName = syntaxSource.ContainingAssembly.Name;
                return;
            }

            var location = syntax.GetLocation().GetLineSpan();
            result.File = syntax.SyntaxTree.FilePath ?? "";
            result.Line = location.StartLinePosition.Line + 1;
            if (string.IsNullOrEmpty(result.ParentHint))
                _installerClassifier.AssignParent(method, result);

            var state = new WalkState(method, model, result);
            state.EntityRoot = entityRoot;
            if (bools != null) {
                foreach (var pair in bools)
                    state.Bools[pair.Key] = pair.Value;
            }

            foreach (var parameter in method.Parameters) {
                state.Locals[parameter.Name] = new WalkLocal {
                    Name = parameter.Name,
                    Type = parameter.Type,
                    IsBuilder = _installerClassifier.IsBuilderLike(parameter.Type),
                };
                if (_installerClassifier.IsBuilderLike(parameter.Type))
                    state.TaintedBuilder.Add(parameter.Name);
            }

            for (var i = 0; i < original.TypeParameters.Length && i < method.TypeArguments.Length; i++)
                state.Substitution[original.TypeParameters[i]] = method.TypeArguments[i];

            if (syntax is MethodDeclarationSyntax methodSyntax) {
                if (methodSyntax.Body != null)
                    _statementWalker.WalkStatement(state, methodSyntax.Body);
                else if (methodSyntax.ExpressionBody != null)
                    _expressionWalker.WalkExpression(state, methodSyntax.ExpressionBody.Expression);
            }
            else if (syntax is LocalFunctionStatementSyntax localSyntax) {
                if (localSyntax.Body != null)
                    _statementWalker.WalkStatement(state, localSyntax.Body);
                else if (localSyntax.ExpressionBody != null)
                    _expressionWalker.WalkExpression(state, localSyntax.ExpressionBody.Expression);
            }
            else if (syntax is AccessorDeclarationSyntax)
                _walkDiagnostics.Error(state, syntax, "accessor");

            if (_installerClassifier.IsRegistrationLike(method.ReturnType) && state.LastRegistration != null)
                result.ReturnedOrdinal = state.LastRegistration.Ordinal;
        }
    }
}
