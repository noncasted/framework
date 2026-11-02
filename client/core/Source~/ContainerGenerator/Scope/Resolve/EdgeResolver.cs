using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class EdgeResolver {
        public static IReadOnlyList<ScopeGraph> Resolve(
            GraphDocument document,
            Compilation compilation,
            ReferenceSymbols references) {
            var graphs = new List<ScopeGraph>();
            if (document == null)
                return graphs;

            ParentBinding.BindParents(document);
            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                if (method.IsRoot == false)
                    continue;
                if (IsLocalRoot(method, compilation) == false)
                    continue;
                graphs.Add(ResolveRoot(document, method, compilation, references));
            }

            return graphs;
        }

        public static ScopeGraph? ResolveByHint(
            GraphDocument document,
            Compilation compilation,
            ReferenceSymbols references,
            string hint) {
            if (document == null || string.IsNullOrEmpty(hint))
                return null;

            GraphMethod? match = null;
            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                if (method.IsRoot == false)
                    continue;
                if (IsLocalRoot(method, compilation) == false)
                    continue;
                if (method.Id.IndexOf(hint, StringComparison.Ordinal) < 0)
                    continue;
                if (match != null)
                    return null;
                match = method;
            }

            if (match == null)
                return null;

            ParentBinding.BindParents(document);
            return ResolveRoot(document, match, compilation, references);
        }

        public static ScopeGraph ResolveRoot(
            GraphDocument document,
            GraphMethod root,
            Compilation compilation,
            ReferenceSymbols references) {
            var graph = new ScopeGraph {
                RootId = root.Id,
                ParentId = root.ParentId,
                Variant = root.Variant,
                ViewType = root.ViewType,
            };
            RegistrationFlattener.Flatten(document, root, graph.Registrations);
            if (root.ParentUnresolved)
                graph.MarkParentMissing();

            var index = new TypeIndex(compilation);
            var implicitTypes = ServiceIndex.ImplicitTypes(compilation, index);
            LoaderServiceHoles.EnsureLoaderServices(graph, compilation, index);
            var lastByType = new Dictionary<string, int>(StringComparer.Ordinal);
            var allByType = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var parentExports = ParentExports.CollectParentExports(document, root, graph);

            for (var i = 0; i < graph.Registrations.Count; i++)
                ServiceIndex.IndexServices(graph.Registrations[i], i, lastByType, allByType);

            var context = new BindContext(graph, index, compilation, references, implicitTypes, lastByType, allByType, parentExports);
            for (var i = 0; i < graph.Registrations.Count; i++)
                RegistrationBinder.Bind(context, i);

            AlternativeLinks.LinkAlternatives(graph, allByType);
            PhaseListeners.ReportTransientPhaseListeners(graph);
            DependencyOrder.DetectCycles(graph);
            graph.ConstructionOrder.AddRange(DependencyOrder.SortTopologically(graph));
            return graph;
        }

        private static bool IsLocalRoot(GraphMethod method, Compilation compilation) {
            if (method == null || compilation == null)
                return false;
            if (string.IsNullOrEmpty(method.AssemblyName) == false &&
                string.Equals(method.AssemblyName, compilation.AssemblyName, StringComparison.Ordinal) == false)
                return false;
            return true;
        }
    }
}
