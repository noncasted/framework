using System.Collections.Generic;
using System.Globalization;

namespace ContainerGenerator {
    internal static class ParameterBinder {
        public static void BindParameters(
            ScopeGraph graph,
            int ownerIndex,
            EquatableArray<ParameterModel> parameters,
            string source,
            TypeIndex types,
            HashSet<string> implicitTypes,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType,
            Dictionary<string, string> parentExports,
            int arm,
            string armParameterType,
            string method = "") {
            var registration = graph.Registrations[ownerIndex];
            for (var i = 0; i < parameters.Count; i++) {
                var parameter = parameters[i];
                var edge = new GraphEdge {
                    ParameterName = parameter.Name,
                    ParameterType = parameter.TypeFullName,
                    Source = source,
                    Method = method,
                    Arm = arm,
                };

                if (string.IsNullOrEmpty(armParameterType) == false && types.IsAssignable(armParameterType, parameter.TypeFullName)) {
                    // WithParameter of its own arm: at runtime the builder holds only the value of the chosen arm.
                    edge.Kind = "ArmParameter";
                }
                else if (TakesOwnParameter(graph, registration, parameter, types)) {
                    // The value of its own WithParameter wins over any registration of the type and over what the
                    // scope gives by itself (the container, the lifetimes).
                    edge.Kind = arm >= 0 ? "ArmParameter" : "Hole";
                }
                else if (lastByType.TryGetValue(parameter.TypeFullName, out var target)) {
                    edge.Kind = "Registration";
                    edge.TargetIndex = target;
                    WarnIfAmbiguous(graph, registration, parameter, target, allByType);
                }
                else if (CollectionTypes.TryElement(parameter.TypeFullName, out var element) &&
                         allByType.TryGetValue(element, out var indices)) {
                    edge.Kind = "Collection";
                    edge.CollectionIndices.AddRange(indices);
                    if (indices.Count > 0)
                        edge.TargetIndex = indices[indices.Count - 1];
                }
                else if (implicitTypes.Contains(parameter.TypeFullName)) {
                    edge.Kind = "Implicit";
                }
                else if (CollectionTypes.TryElement(parameter.TypeFullName, out _) &&
                         parentExports.ContainsKey(parameter.TypeFullName) == false) {
                    // A collection is always ResolveAll of its own scope (like ContainerLocal<IReadOnlyList<T>>): an empty list.
                    edge.Kind = "Collection";
                }
                else if (parentExports.TryGetValue(parameter.TypeFullName, out var parentId)) {
                    edge.Kind = "Hole";
                    edge.ParentRootId = parentId;
                }
                else if (CollectionTypes.TryElement(parameter.TypeFullName, out _)) {
                    // No registration of the element in the scope: an empty array, as ResolveAll gives.
                    edge.Kind = "Collection";
                }
                else if (graph.ParentMissing) {
                    edge.Kind = "Missing";
                }
                else {
                    edge.Kind = "Missing";
                    graph.Diagnostics.Add(ResolveDiagnostics.Missing(registration, parameter));
                }

                registration.Dependencies.Add(edge);
            }
        }

        // The exact type first, then a value the parameter can hold (ILifetime for IReadOnlyLifetime, a class for its
        // interface), as ContainerBuilder.TryGetParameter takes it. Two such values: nothing tells which one is meant.
        private static bool TakesOwnParameter(
            ScopeGraph graph,
            GraphRegistration registration,
            ParameterModel parameter,
            TypeIndex types) {
            if (registration.Kind != "Register" && registration.Kind != "SwitchFactory")
                return false;

            if (registration.ParameterTypes.Contains(parameter.TypeFullName))
                return true;

            var assignable = 0;
            for (var i = 0; i < registration.ParameterTypes.Count; i++) {
                if (types.IsAssignable(registration.ParameterTypes[i], parameter.TypeFullName))
                    assignable++;
            }

            if (assignable > 1) {
                graph.Diagnostics.Add(new DiagnosticInfo(
                    GraphDescriptors.AmbiguousParameter,
                    registration.Location,
                    parameter.Name,
                    parameter.TypeFullName,
                    registration.ImplementationType,
                    registration.File ?? "",
                    registration.Line.ToString(CultureInfo.InvariantCulture)));
            }

            return assignable > 0;
        }

        // A single parameter of a type registered several times takes the last registration (Resolve does the same):
        // usually one of them is a mistake, or the parameter wants all of them.
        private static void WarnIfAmbiguous(
            ScopeGraph graph,
            GraphRegistration owner,
            ParameterModel parameter,
            int target,
            Dictionary<string, List<int>> allByType) {
            if (allByType.TryGetValue(parameter.TypeFullName, out var indices) == false)
                return;

            // The branches of one if / switch are one registration at runtime.
            var count = 0;
            var hasAlternative = false;
            for (var i = 0; i < indices.Count; i++) {
                if (graph.Registrations[indices[i]].Origin == "Alternative")
                    hasAlternative = true;
                else
                    count++;
            }

            if (hasAlternative)
                count++;
            if (count < 2)
                return;

            graph.Diagnostics.Add(new DiagnosticInfo(
                GraphDescriptors.AmbiguousRegistration,
                owner.Location,
                parameter.Name,
                string.IsNullOrEmpty(owner.ImplementationType) ? owner.Kind : owner.ImplementationType,
                count.ToString(CultureInfo.InvariantCulture),
                parameter.TypeFullName,
                ResolveDiagnostics.Display(graph.Registrations[target]),
                owner.File ?? "",
                owner.Line.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
