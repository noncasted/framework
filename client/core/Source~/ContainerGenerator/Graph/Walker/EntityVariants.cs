using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class EntityVariants {
        private readonly Compilation _compilation;
        private readonly UnenumerableConditions _unenumerableConditions;
        private readonly IGraphWalk _walk;
        private readonly WalkDiagnostics _walkDiagnostics;

        public EntityVariants(Compilation compilation, UnenumerableConditions unenumerableConditions, IGraphWalk walk, WalkDiagnostics walkDiagnostics) {
            _compilation = compilation;
            _unenumerableConditions = unenumerableConditions;
            _walk = walk;
            _walkDiagnostics = walkDiagnostics;
        }

        public void AttachEntityScopes() {
            var sites = new List<EntityLoadSite>();
            foreach (var tree in _compilation.SyntaxTrees) {
                var model = _compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot().DescendantNodes()) {
                    if (node is not InvocationExpressionSyntax invocation)
                        continue;
                    var symbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (symbol == null || IsEntityLoad(symbol) == false)
                        continue;

                    var viewIndex = ParameterIndex(symbol, "view");
                    var constructIndex = ParameterIndex(symbol, "construct");
                    if (viewIndex < 0 || constructIndex < 0)
                        continue;
                    if (invocation.ArgumentList.Arguments.Count <= viewIndex ||
                        invocation.ArgumentList.Arguments.Count <= constructIndex)
                        continue;

                    var construct = ResolveConstruct(
                        model,
                        invocation.ArgumentList.Arguments[constructIndex].Expression);
                    if (construct == null)
                        continue;

                    // No class can be found by a lambda: the scope of this entity is not built at all.
                    if (construct.MethodKind == MethodKind.AnonymousFunction) {
                        _walkDiagnostics.ReportLambdaRoot(
                            WalkSyntax.Unwrap(invocation.ArgumentList.Arguments[constructIndex].Expression), symbol.Name);
                        continue;
                    }

                    var viewExpression = invocation.ArgumentList.Arguments[viewIndex].Expression;
                    var viewType = model.GetTypeInfo(viewExpression).Type;
                    var site = new EntityLoadSite {
                        Construct = construct,
                        ViewType = viewType == null ? "" : TypeNames.ForMetadata(viewType),
                    };
                    VariantConditions.CollectAssignments(invocation, site.Bools);
                    sites.Add(site);
                }
            }

            var groups = new Dictionary<string, List<EntityLoadSite>>(StringComparer.Ordinal);
            for (var i = 0; i < sites.Count; i++) {
                var id = MethodIds.Of(sites[i].Construct);
                if (groups.TryGetValue(id, out var list) == false) {
                    list = new List<EntityLoadSite>();
                    groups[id] = list;
                }

                list.Add(sites[i]);
            }

            foreach (var pair in groups) {
                if (InstallerClassifier.IsGeneric(pair.Value[0].Construct)) {
                    _walkDiagnostics.ReportGenericRoot(pair.Value[0].Construct);
                    continue;
                }

                _unenumerableConditions.ReportUnenumerableVariants(pair.Value[0].Construct);
                var distinct = DistinctViewTypes(pair.Value);
                // The loader finds the class by the bare name of the method (GeneratedScopes.RootId).
                if (MethodIds.HasOverloadSuffix(pair.Key))
                    _walkDiagnostics.ReportOverloadedRoot(pair.Key, pair.Value[0].Construct);

                var origin = _walk.WalkMethod(pair.Value[0].Construct, distinct.Count <= 1);
                if (distinct.Count <= 1) {
                    origin.SetViewType(pair.Value[0].ViewType);
                    origin.SetRoot(true);
                    continue;
                }

                origin.SetRoot(false);
                var names = VariantNames(distinct);
                var built = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < pair.Value.Count; i++) {
                    var site = pair.Value[i];
                    if (string.IsNullOrEmpty(site.ViewType) || built.Add(site.ViewType) == false)
                        continue;
                    var variant = names[site.ViewType];
                    _walk.WalkVariant(site.Construct, origin.Id + "+" + variant, variant, site.ViewType, site.Bools);
                }
            }
        }

        private static List<string> DistinctViewTypes(List<EntityLoadSite> sites) {
            var types = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < sites.Count; i++) {
                var type = sites[i].ViewType;
                if (string.IsNullOrEmpty(type) || seen.Add(type) == false)
                    continue;
                types.Add(type);
            }

            return types;
        }

        // One name per view type: Local / Remote while they name one type, the name of the type when they do not.
        private static Dictionary<string, string> VariantNames(List<string> viewTypes) {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < viewTypes.Count; i++)
                names[viewTypes[i]] = VariantName(viewTypes[i]);
            RenameCollisions(names, ScopeNames.Short);
            RenameCollisions(names, TypeNames.SafeHint);
            return names;
        }

        private static void RenameCollisions(Dictionary<string, string> names, Func<string, string> rename) {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in names) {
                counts.TryGetValue(pair.Value, out var count);
                counts[pair.Value] = count + 1;
            }

            foreach (var viewType in new List<string>(names.Keys)) {
                if (counts[names[viewType]] > 1)
                    names[viewType] = rename(viewType);
            }
        }

        private static string VariantName(string viewType) {
            var shortName = ScopeNames.Short(viewType);
            if (shortName.IndexOf("Local", StringComparison.Ordinal) >= 0)
                return "Local";
            if (shortName.IndexOf("Remote", StringComparison.Ordinal) >= 0)
                return "Remote";
            return string.IsNullOrEmpty(shortName) ? "Variant" : shortName;
        }

        private static bool IsEntityLoad(IMethodSymbol symbol) {
            if (symbol.Name != "Load")
                return false;
            var type = symbol.ContainingType;
            if (type == null)
                return false;
            if (type.Name == "IEntityScopeLoader" || type.Name == "EntityScopeLoader")
                return true;
            foreach (var implemented in type.AllInterfaces) {
                if (implemented.Name == "IEntityScopeLoader")
                    return true;
            }

            return false;
        }

        private static int ParameterIndex(IMethodSymbol symbol, string name) {
            for (var i = 0; i < symbol.Parameters.Length; i++) {
                if (symbol.Parameters[i].Name == name)
                    return i;
            }

            return -1;
        }

        private IMethodSymbol? ResolveConstruct(SemanticModel model, ExpressionSyntax expression) {
            expression = WalkSyntax.Unwrap(expression);
            var symbol = model.GetSymbolInfo(expression).Symbol as IMethodSymbol;
            if (symbol != null)
                return symbol;
            if (expression is IdentifierNameSyntax identifier)
                return model.GetSymbolInfo(identifier).Symbol as IMethodSymbol;
            return null;
        }

        private sealed class EntityLoadSite {
            public IMethodSymbol Construct = null!;
            public string ViewType = "";
            public Dictionary<string, bool> Bools = new Dictionary<string, bool>(StringComparer.Ordinal);
        }
    }
}
