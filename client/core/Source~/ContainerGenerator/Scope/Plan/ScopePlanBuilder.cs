using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class ScopePlanBuilder {
        public static ScopePlan? Build(
            ScopeGraph graph,
            GraphDocument document,
            Compilation compilation,
            ReferenceSymbols references) {
            if (graph == null || document == null)
                return null;

            GraphMethod? root = null;
            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id == graph.RootId) {
                    root = document.Methods[i];
                    break;
                }
            }

            if (root == null)
                throw new InvalidOperationException("Scope root '" + graph.RootId + "' is not in the graph document.");
            if (ManifestHarvest.IsHarvest(graph.RootId))
                return null;
            if (HasEmitBlockingDiagnostic(graph, document, root))
                return null;

            var lookupId = string.IsNullOrEmpty(root.OriginId) ? graph.RootId : root.OriginId;
            var method = MethodIds.Find(compilation, lookupId) ?? MethodIds.Find(compilation, graph.RootId);
            if (method == null)
                throw new InvalidOperationException(
                    "Scope root '" + graph.RootId + "' has no source in this assembly: the assembly that declares it has to build the scope.");

            var plan = new ScopePlan { RootId = graph.RootId };
            ScopeNames.ParseRoot(graph.RootId, out plan.Namespace, out var typeName, out var methodName, out plan.ClassName);
            // Named by the outer method while it has one root. Variants and several roots that are local
            // functions of one method are named by the local function, otherwise the class and the hint would collide.
            if (string.IsNullOrEmpty(graph.Variant) == false || HasSiblingRoots(document, graph.RootId)) {
                var constructName = ConstructMethodName(graph.RootId, methodName);
                plan.ClassName = typeName + constructName + graph.Variant + "Container";
            }

            plan.HintName = plan.ClassName + ".g.cs";
            plan.ViewType = graph.ViewType ?? "";
            plan.OriginRootId = graph.RootId;
            if (string.IsNullOrEmpty(graph.Variant) == false &&
                graph.RootId.EndsWith("+" + graph.Variant, StringComparison.Ordinal))
                plan.OriginRootId = graph.RootId.Substring(0, graph.RootId.Length - graph.Variant.Length - 1);
            plan.HasContainerInterface = compilation.GetTypeByMetadataName("Internal.IContainer") != null;
            plan.HasUnityGameObject = compilation.GetTypeByMetadataName("UnityEngine.GameObject") != null;
            plan.HasDiagnosticsType = compilation.GetTypeByMetadataName("Internal.IContainerDiagnostics") != null;
            plan.HasRegistrationInfo = compilation.GetTypeByMetadataName("Internal.RegistrationInfo") != null;
            plan.HasServiceLifetime = compilation.GetTypeByMetadataName("Internal.ServiceLifetime") != null;
            plan.HasRuntimeInitialize = references.RuntimeInitialize != null;
            plan.HasContainerDiagnostics = plan.HasDiagnosticsType && plan.HasRegistrationInfo && plan.HasServiceLifetime &&
                                           compilation.GetTypeByMetadataName("Internal.ContainerDiagnostics") != null &&
                                           compilation.GetTypeByMetadataName("Internal.ContainerRegistryDebug") != null;
            plan.ProvidesDefinition = compilation.GetTypeByMetadataName("Internal.IProvides`1");
            plan.HasProvides = plan.ProvidesDefinition != null;

            var types = new TypeIndex(compilation);
            var used = new HashSet<string>(StringComparer.Ordinal);
            used.Add(plan.LifetimeField);
            used.Add(plan.LifetimeParam);
            used.Add(plan.ParentField);
            used.Add(plan.ParentParam);
            used.Add("_exports");
            used.Add("GetExport");
            used.Add("_diagnostics");
            used.Add("_registrationInfos");
            used.Add("_buildOrder");
            used.Add("_disposed");
            used.Add("_isDisposing");
            used.Add("errors");
            used.Add("alternative");
            used.Add("created");

            PlanSlots.BuildSlots(plan, graph, types, used);
            PlanSlots.GroupAlternatives(plan, graph);
            PlanFields.AssignFields(plan, graph, used);
            PlanHoles.AssignHoles(plan, graph, used);
            PlanMarkers.BuildMarkers(plan, graph, used);
            PlanExports.BuildExports(plan, graph);
            PlanFactories.BuildFactories(plan);
            PlanExports.BuildProvides(plan);
            PlanDisposal.CollectDisposable(plan, graph, types);
            if (plan.HasContainerDiagnostics)
                PlanDiagnostics.BuildDiagnostics(plan, compilation, types);
            return plan;
        }

        private static string ConstructMethodName(string rootId, string fallback) {
            if (string.IsNullOrEmpty(rootId))
                return fallback;
            var first = rootId.IndexOf('+');
            if (first < 0)
                return fallback;
            var rest = rootId.Substring(first + 1);
            var second = rest.IndexOf('+');
            var local = second < 0 ? rest : rest.Substring(0, second);
            return string.IsNullOrEmpty(local) ? fallback : local;
        }

        private static bool HasSiblingRoots(GraphDocument document, string rootId) {
            var plus = rootId.IndexOf('+');
            if (plus < 0)
                return false;

            var outer = rootId.Substring(0, plus + 1);
            for (var i = 0; i < document.Methods.Count; i++) {
                var other = document.Methods[i];
                if (other.IsRoot == false || other.Id == rootId || ManifestHarvest.IsHarvest(other.Id))
                    continue;
                if (other.Id.StartsWith(outer, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasEmitBlockingDiagnostic(ScopeGraph graph, GraphDocument document, GraphMethod root) {
            for (var i = 0; i < graph.Diagnostics.Count; i++) {
                var id = graph.Diagnostics[i].Descriptor.Id;
                if (id == "CINGR003" || id == "CINGR004" || id == "CINGR001" || id == "CINGR002" ||
                    id == "CINGR005" || id == "CINGR007" || id == "CINGR010" || id == "CINGR011" ||
                    id == "CINGR012")
                    return true;
            }

            var methods = new HashSet<string>(StringComparer.Ordinal);
            CollectMethods(document, root, methods);
            for (var i = 0; i < document.Diagnostics.Count; i++) {
                var diagnostic = document.Diagnostics[i];
                var id = diagnostic.Descriptor.Id;
                if (id == "CINGR007")
                    return true;
                if (id == "CINGR013" && (diagnostic.MessageArgs[0] == root.Id || diagnostic.MessageArgs[0] == root.OriginId))
                    return true;
                if (id != "CINGR001" && id != "CINGR002" && id != "CINGR005")
                    continue;
                if (diagnostic.MessageArgs.Count < 2)
                    return true;
                if (methods.Contains(diagnostic.MessageArgs[1]))
                    return true;
            }

            return false;
        }

        private static void CollectMethods(GraphDocument document, GraphMethod root, HashSet<string> output) {
            var map = new Dictionary<string, GraphMethod>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++)
                map[document.Methods[i].Id] = document.Methods[i];

            Walk(root);

            void Walk(GraphMethod method) {
                if (output.Add(method.Id) == false)
                    return;
                method.PadCallLists();
                for (var i = 0; i < method.Calls.Count; i++) {
                    if (map.TryGetValue(method.Calls[i], out var callee))
                        Walk(callee);
                }
            }
        }
    }
}
