using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class RegistrationBinder {
        public static void Bind(
            ScopeGraph graph,
            int index,
            TypeIndex types,
            Compilation compilation,
            ReferenceSymbols references,
            HashSet<string> implicitTypes,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType,
            Dictionary<string, string> parentExports) {
            var registration = graph.Registrations[index];
            if (registration.Origin == "SceneServices" || registration.Origin == "ExternalInstaller")
                return;

            if (registration.Origin == "SwitchFactory") {
                for (var i = 0; i < registration.Arms.Count; i++) {
                    var armType = types.Find(registration.Arms[i].ImplementationType);
                    if (armType == null) {
                        if (string.IsNullOrEmpty(registration.Arms[i].ImplementationType) == false)
                            graph.Diagnostics.Add(ResolveDiagnostics.MissingType(registration, registration.Arms[i].ImplementationType));
                        continue;
                    }

                    BindType(
                        graph,
                        index,
                        armType,
                        types,
                        compilation,
                        references,
                        implicitTypes,
                        lastByType,
                        allByType,
                        parentExports,
                        i,
                        registration.Arms[i].ParameterType);
                }

                BindDiscriminant(graph, registration, lastByType, parentExports);
                return;
            }

            if (string.IsNullOrEmpty(registration.ImplementationType)) {
                // A registration without a type: the call did not bind (an error in the source), no class is emitted for it.
                if (registration.Origin != LoaderServices.Origin)
                    graph.Diagnostics.Add(ResolveDiagnostics.MissingType(registration, "(unbound " + registration.Kind + ")"));
                return;
            }

            var implementation = types.Find(registration.ImplementationType);
            if (implementation == null) {
                graph.Diagnostics.Add(ResolveDiagnostics.MissingType(registration, registration.ImplementationType));
                return;
            }

            BindType(
                graph,
                index,
                implementation,
                types,
                compilation,
                references,
                implicitTypes,
                lastByType,
                allByType,
                parentExports);
        }

        private static void BindType(
            ScopeGraph graph,
            int ownerIndex,
            INamedTypeSymbol implementation,
            TypeIndex types,
            Compilation compilation,
            ReferenceSymbols references,
            HashSet<string> implicitTypes,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType,
            Dictionary<string, string> parentExports,
            int arm = -1,
            string armParameterType = "") {
            var registration = graph.Registrations[ownerIndex];

            // What the generated class cannot write (a constructor call or an [Inject] call it has no access to)
            // is an error at the registration, not a compile error in the generated file or a silently skipped call.
            var constructs = BindsConstructor(registration.Origin);
            var constructProblems = constructs
                ? TypeAnalyzer.ConstructProblems(implementation, compilation)
                : new List<string>();
            var injectProblems = TypeAnalyzer.InjectProblems(implementation, references, compilation);
            for (var i = 0; i < constructProblems.Count; i++)
                graph.Diagnostics.Add(ResolveDiagnostics.TypeProblem(GraphDescriptors.NotConstructible, registration, implementation, constructProblems[i]));
            for (var i = 0; i < injectProblems.Count; i++)
                graph.Diagnostics.Add(ResolveDiagnostics.TypeProblem(GraphDescriptors.NotInjectable, registration, implementation, injectProblems[i]));
            if (constructProblems.Count > 0 || injectProblems.Count > 0)
                return;

            var model = TypeAnalyzer.Analyze(implementation, references, registration.Location, false);
            if (registration.Origin == "Injectable" && (model == null || string.IsNullOrEmpty(model.ConstructName)))
                graph.Diagnostics.Add(ResolveDiagnostics.NoInjectMethod(registration));
            if (model == null)
                return;

            UnusedParameters.WarnUnusedParameters(graph, registration, implementation, model, constructs, types, armParameterType);

            // An [Inject] without parameters gives no edges: the emitter needs the method name separately.
            if (arm < 0)
                registration.InjectMethod = model.ConstructName;

            if (constructs)
                ParameterBinder.BindParameters(
                    graph,
                    ownerIndex,
                    model.ConstructorParameters,
                    "Constructor",
                    types,
                    implicitTypes,
                    lastByType,
                    allByType,
                    parentExports,
                    arm,
                    armParameterType);

            ParameterBinder.BindParameters(
                graph,
                ownerIndex,
                model.ConstructParameters,
                "Construct",
                types,
                implicitTypes,
                lastByType,
                allByType,
                parentExports,
                arm,
                armParameterType,
                model.ConstructName);
        }

        // A switch over an enum builds the arm by the value registered in the scope (RegisterInstance(definition.Type)).
        private static void BindDiscriminant(
            ScopeGraph graph,
            GraphRegistration registration,
            Dictionary<string, int> lastByType,
            Dictionary<string, string> parentExports) {
            var type = registration.InferDiscriminantType();
            var edge = new GraphEdge {
                ParameterName = "discriminant",
                ParameterType = type,
                Source = "Discriminant",
            };

            if (lastByType.TryGetValue(type, out var target)) {
                edge.Kind = "Registration";
                edge.TargetIndex = target;
            }
            else if (parentExports.TryGetValue(type, out var parentId)) {
                edge.Kind = "Hole";
                edge.ParentRootId = parentId;
            }
            else {
                edge.Kind = "Missing";
                graph.Diagnostics.Add(ResolveDiagnostics.Missing(registration, new ParameterModel(type, "discriminant")));
            }

            registration.Dependencies.Add(edge);
        }

        private static bool BindsConstructor(string origin) {
            return origin == "ConstructedType" ||
                   origin == "Alternative" ||
                   origin == "ParameterHole" ||
                   origin == "SwitchFactory" ||
                   origin == LoaderServices.Origin;
        }
    }
}
