using System.Globalization;

namespace ContainerGenerator {
    internal static class ParameterBinder {
        public static void BindParameters(
            BindContext context,
            int ownerIndex,
            EquatableArray<ParameterModel> parameters,
            string source,
            int arm,
            string armParameterType,
            string method = "") {
            var graph = context.Graph;
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

                if (string.IsNullOrEmpty(armParameterType) == false && context.Types.IsAssignable(armParameterType, parameter.TypeFullName)) {
                    // WithParameter of its own arm: at runtime the builder holds only the value of the chosen arm.
                    edge.Bind("ArmParameter");
                }
                else if (TakesOwnParameter(context, registration, parameter)) {
                    // The value of its own WithParameter wins over any registration of the type and over what the
                    // scope gives by itself (the container, the lifetimes).
                    edge.Bind(arm >= 0 ? "ArmParameter" : "Hole");
                }
                else if (context.LastByType.TryGetValue(parameter.TypeFullName, out var target)) {
                    edge.BindRegistration(target);
                    WarnIfAmbiguous(context, registration, parameter, target);
                }
                else if (CollectionTypes.TryElement(parameter.TypeFullName, out var element) &&
                         context.AllByType.TryGetValue(element, out var indices)) {
                    edge.BindCollection(indices);
                }
                else if (context.ImplicitTypes.Contains(parameter.TypeFullName)) {
                    edge.Bind("Implicit");
                }
                else if (CollectionTypes.TryElement(parameter.TypeFullName, out _) &&
                         context.ParentExports.ContainsKey(parameter.TypeFullName) == false) {
                    // A collection is always ResolveAll of its own scope (like ContainerLocal<IReadOnlyList<T>>): an empty list.
                    edge.Bind("Collection");
                }
                else if (context.ParentExports.TryGetValue(parameter.TypeFullName, out var parentId)) {
                    edge.BindParent(parentId);
                }
                else if (CollectionTypes.TryElement(parameter.TypeFullName, out _)) {
                    // No registration of the element in the scope: an empty array, as ResolveAll gives.
                    edge.Bind("Collection");
                }
                else if (graph.ParentMissing) {
                    edge.Bind("Missing");
                }
                else {
                    edge.Bind("Missing");
                    graph.Diagnostics.Add(ResolveDiagnostics.Missing(registration, parameter));
                }

                registration.Dependencies.Add(edge);
            }
        }

        // The exact type first, then a value the parameter can hold (ILifetime for IReadOnlyLifetime, a class for its
        // interface), as ContainerBuilder.TryGetParameter takes it. Two such values: nothing tells which one is meant.
        private static bool TakesOwnParameter(BindContext context, GraphRegistration registration, ParameterModel parameter) {
            if (registration.Kind != "Register" && registration.Kind != "SwitchFactory")
                return false;

            if (registration.ParameterTypes.Contains(parameter.TypeFullName))
                return true;

            var assignable = 0;
            for (var i = 0; i < registration.ParameterTypes.Count; i++) {
                if (context.Types.IsAssignable(registration.ParameterTypes[i], parameter.TypeFullName))
                    assignable++;
            }

            if (assignable > 1) {
                context.Graph.Diagnostics.Add(new DiagnosticInfo(
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
        private static void WarnIfAmbiguous(BindContext context, GraphRegistration owner, ParameterModel parameter, int target) {
            if (context.AllByType.TryGetValue(parameter.TypeFullName, out var indices) == false)
                return;

            var graph = context.Graph;

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
