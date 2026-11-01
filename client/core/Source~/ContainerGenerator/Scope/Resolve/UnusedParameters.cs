using System.Globalization;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class UnusedParameters {
        // At runtime a value of WithParameter belongs to a constructed registration (ContainerBuilder.TryGetParameter).
        // A WithParameter value no parameter can hold goes nowhere: usually a leftover of a removed parameter.
        public static void WarnUnusedParameters(
            ScopeGraph graph,
            GraphRegistration registration,
            INamedTypeSymbol implementation,
            InjectorModel model,
            bool constructs,
            TypeIndex types,
            string armParameterType) {
            // A switch holds the values of all its arms: each arm answers for its own value only.
            if (registration.Origin == "SwitchFactory") {
                if (string.IsNullOrEmpty(armParameterType) == false)
                    WarnIfUnused(graph, registration, implementation, model, constructs, types, armParameterType);
                return;
            }

            if (registration.Kind != "Register")
                return;

            for (var i = 0; i < registration.ParameterTypes.Count; i++)
                WarnIfUnused(graph, registration, implementation, model, constructs, types, registration.ParameterTypes[i]);
        }

        private static void WarnIfUnused(
            ScopeGraph graph,
            GraphRegistration registration,
            INamedTypeSymbol implementation,
            InjectorModel model,
            bool constructs,
            TypeIndex types,
            string type) {
            // A type the index cannot read (an open generic of an installer) says nothing about the parameters.
            if (types.IsKnown(type) == false)
                return;

            if (constructs && CanTake(model.ConstructorParameters, type, types))
                return;

            if (CanTake(model.ConstructParameters, type, types))
                return;

            graph.Diagnostics.Add(new DiagnosticInfo(
                GraphDescriptors.UnusedParameter,
                registration.Location,
                type,
                types.Format(implementation),
                registration.File ?? "",
                registration.Line.ToString(CultureInfo.InvariantCulture)));
        }

        private static bool CanTake(EquatableArray<ParameterModel> parameters, string type, TypeIndex types) {
            for (var i = 0; i < parameters.Count; i++) {
                if (types.IsAssignable(type, parameters[i].TypeFullName))
                    return true;
            }

            return false;
        }
    }
}
