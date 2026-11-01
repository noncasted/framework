using System;
using System.Collections.Generic;
using System.Globalization;
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

            BindParents(document);
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

            BindParents(document);
            return ResolveRoot(document, match, compilation, references);
        }

        public static void BindParents(GraphDocument document) {
            if (document == null)
                return;

            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                var hint = method.ParentHint;
                if (string.IsNullOrEmpty(hint))
                    hint = method.ParentId;
                if (string.IsNullOrEmpty(hint))
                    continue;

                var id = MatchParent(document, hint);
                if (string.IsNullOrEmpty(id)) {
                    method.ParentUnresolved = true;
                    // A root from the manifest of another assembly: its parent is checked by the assembly that owns it.
                    if (string.IsNullOrEmpty(method.AssemblyName) == false &&
                        string.Equals(method.AssemblyName, document.AssemblyName, StringComparison.Ordinal) == false)
                        continue;
                    document.Diagnostics.Add(MissingParentManifest(
                        hint,
                        method.ParentAssembly,
                        method.Id));
                    continue;
                }

                method.ParentId = id;
            }
        }

        public static string MatchParent(GraphDocument document, string hint) {
            if (document == null || string.IsNullOrEmpty(hint))
                return "";

            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id == hint)
                    return document.Methods[i].Id;
            }

            return "";
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
            Flatten(document, root, graph.Registrations);
            graph.ParentMissing = root.ParentUnresolved;

            var index = new TypeIndex(compilation);
            var implicitTypes = ImplicitTypes(compilation, index);
            EnsureLoaderServices(graph, compilation, index);
            var lastByType = new Dictionary<string, int>(StringComparer.Ordinal);
            var allByType = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var parentExports = CollectParentExports(document, root, graph);

            for (var i = 0; i < graph.Registrations.Count; i++)
                IndexServices(graph.Registrations[i], i, lastByType, allByType);

            for (var i = 0; i < graph.Registrations.Count; i++)
                Bind(graph, i, index, compilation, references, implicitTypes, lastByType, allByType, parentExports);

            LinkAlternatives(graph, allByType);
            ReportTransientPhaseListeners(graph);
            DetectCycles(graph);
            graph.ConstructionOrder.AddRange(SortTopologically(graph));
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

        private static void Flatten(
            GraphDocument document,
            GraphMethod root,
            List<GraphRegistration> output) {
            var methods = new Dictionary<string, GraphMethod>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++)
                methods[document.Methods[i].Id] = document.Methods[i];

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack = new HashSet<string>(StringComparer.Ordinal);
            var walks = 0;
            Walk(root, new List<string>(), "", "");

            void Walk(GraphMethod method, List<string> typeArgs, string asServices, string hole, string parameters = "") {
                var walk = walks++;
                var substitution = typeArgs ?? new List<string>();
                var generic = substitution.Count > 0;
                if (stack.Contains(method.Id))
                    return;
                if (generic == false && visited.Add(method.Id) == false)
                    return;

                stack.Add(method.Id);
                GraphRegistration? returned = null;
                var steps = OrderedSteps(method);
                for (var i = 0; i < steps.Count; i++) {
                    var step = steps[i];
                    if (step.Registration != null) {
                        var clone = TypeSubstitution.Instantiate(step.Registration, substitution);
                        // The statements of a method are numbered inside it, a generic method is walked per call.
                        if (string.IsNullOrEmpty(clone.BranchPath) == false)
                            clone.BranchPath = walk + "/" + clone.BranchPath;
                        if (method.ReturnedOrdinal == step.Registration.Ordinal)
                            returned = clone;
                        output.Add(clone);
                    }

                    if (step.CallId != null && methods.TryGetValue(step.CallId, out var callee)) {
                        var callArgs = TypeSubstitution.DecodeTypeArgs(method.CallTypesAt(step.CallIndex));
                        if (substitution.Count > 0) {
                            for (var a = 0; a < callArgs.Count; a++)
                                callArgs[a] = TypeSubstitution.ReplacePlaceholders(callArgs[a], substitution);
                        }

                        Walk(
                            callee,
                            callArgs,
                            TypeSubstitution.ReplacePlaceholders(method.CallAsAt(step.CallIndex), substitution),
                            TypeSubstitution.ReplacePlaceholders(method.CallHoleAt(step.CallIndex), substitution),
                            TypeSubstitution.ReplacePlaceholders(method.CallParametersAt(step.CallIndex), substitution));
                    }
                }

                if (returned != null) {
                    ApplyCallAs(returned, asServices);
                    if (string.IsNullOrEmpty(parameters) == false)
                        returned.ParameterTypes.AddRange(parameters.Split(';'));
                    if (string.IsNullOrEmpty(hole) == false) {
                        returned.Hole = string.IsNullOrEmpty(returned.Hole) ? hole : returned.Hole + "; " + hole;
                        if (returned.Arms.Count == 0 && returned.Origin == "ConstructedType")
                            returned.Origin = "ParameterHole";
                    }
                }

                stack.Remove(method.Id);
            }
        }

        private static void ApplyCallAs(GraphRegistration registration, string asServices) {
            if (string.IsNullOrEmpty(asServices))
                return;

            var parts = asServices.Split(';');
            for (var i = 0; i < parts.Length; i++) {
                var service = parts[i].Trim();
                if (string.IsNullOrEmpty(service))
                    continue;
                if (registration.ServiceTypes.Contains(service))
                    continue;
                registration.ServiceTypes.Add(service);
            }
        }

        private static List<Step> OrderedSteps(GraphMethod method) {
            var steps = new List<Step>();
            for (var i = 0; i < method.Registrations.Count; i++)
                steps.Add(new Step(method.Registrations[i].Ordinal, steps.Count, method.Registrations[i], null, -1));

            method.PadCallLists();
            var callCount = method.Calls.Count;
            var ordinalCount = method.CallOrdinals.Count;
            for (var i = 0; i < callCount; i++) {
                var ordinal = i < ordinalCount ? method.CallOrdinals[i] : int.MaxValue - callCount + i;
                steps.Add(new Step(ordinal, steps.Count, null, method.Calls[i], i));
            }

            // Stable: the scene services share the ordinal of the scene call and keep the order of SceneServicesFactory.
            steps.Sort((a, b) => a.Ordinal != b.Ordinal
                ? a.Ordinal.CompareTo(b.Ordinal)
                : a.Sequence.CompareTo(b.Sequence));
            return steps;
        }

        private static void IndexServices(
            GraphRegistration registration,
            int index,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType) {
            // Injectable is not a service: nobody resolves it, the scope only injects a ready instance.
            if (registration.Origin == "Injectable")
                return;

            var added = false;
            for (var i = 0; i < registration.ServiceTypes.Count; i++) {
                AddService(registration.ServiceTypes[i], index, lastByType, allByType);
                added = true;
            }

            if (added == false && string.IsNullOrEmpty(registration.ImplementationType) == false)
                AddService(registration.ImplementationType, index, lastByType, allByType);
        }

        private static void AddService(
            string type,
            int index,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType) {
            if (string.IsNullOrEmpty(type))
                return;

            lastByType[type] = index;
            if (allByType.TryGetValue(type, out var list) == false) {
                list = new List<int>();
                allByType[type] = list;
            }

            list.Add(index);
        }

        private static Dictionary<string, string> CollectParentExports(
            GraphDocument document,
            GraphMethod root,
            ScopeGraph graph) {
            var exports = new Dictionary<string, string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = root.ParentId;
            while (string.IsNullOrEmpty(current) == false && visited.Add(current)) {
                GraphMethod? method = FindMethod(document, current);
                if (method == null) {
                    graph.ParentMissing = true;
                    graph.Diagnostics.Add(MissingParentManifest(
                        current,
                        ParentAssemblyOf(document, root, current),
                        root.Id));
                    return exports;
                }

                var registrations = new List<GraphRegistration>();
                Flatten(document, method, registrations);
                for (var i = 0; i < registrations.Count; i++)
                    IndexParentServices(registrations[i], current, exports);

                current = string.IsNullOrEmpty(method.ParentId) ? method.ParentHint : method.ParentId;
            }

            return exports;
        }

        private static GraphMethod? FindMethod(GraphDocument document, string id) {
            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id == id)
                    return document.Methods[i];
            }

            return null;
        }

        private static string ParentAssemblyOf(GraphDocument document, GraphMethod root, string parentId) {
            if (string.IsNullOrEmpty(root.ParentAssembly) == false &&
                (root.ParentId == parentId || root.ParentHint == parentId))
                return root.ParentAssembly;

            var method = FindMethod(document, parentId);
            if (method != null && string.IsNullOrEmpty(method.AssemblyName) == false)
                return method.AssemblyName;
            return root.ParentAssembly;
        }

        private static DiagnosticInfo MissingParentManifest(string parentId, string assembly, string declaredOn) {
            return new DiagnosticInfo(
                GraphDescriptors.UnresolvedParent,
                null,
                parentId ?? "",
                string.IsNullOrEmpty(assembly) ? "" : assembly,
                declaredOn ?? "");
        }

        private static void EnsureLoaderServices(ScopeGraph graph, Compilation compilation, TypeIndex types) {
            for (var i = 0; i < LoaderServices.Implementations.Length; i++) {
                if (LoaderServices.Implementations[i] == LoaderServices.EventLoopImplementation) {
                    EnsureEventLoopHole(graph, compilation, types);
                    continue;
                }

                var implementation = LoaderServices.Find(compilation, LoaderServices.Implementations[i]);
                if (implementation == null)
                    continue;

                var implementationName = types.Format(implementation);
                if (string.IsNullOrEmpty(implementationName) || HasService(graph, implementationName))
                    continue;

                var registration = new GraphRegistration {
                    Kind = "Register",
                    ImplementationType = implementationName,
                    Lifetime = "Scoped",
                    Origin = LoaderServices.Origin,
                    Source = "Loader",
                };
                var serviceNames = LoaderServices.Services[i];
                for (var s = 0; s < serviceNames.Length; s++) {
                    var service = LoaderServices.Find(compilation, serviceNames[s]);
                    var formatted = types.Format(service);
                    if (string.IsNullOrEmpty(formatted) == false)
                        registration.ServiceTypes.Add(formatted);
                }

                if (HasAnyService(graph, registration))
                    continue;
                graph.Registrations.Add(registration);
            }
        }

        // IEventLoop is the very builder.Events of the loader: AddBeforeBuild / AddAfterDispose hang on it
        // and the loader runs the phases through it. An instance of the scope class would be another loop.
        private static void EnsureEventLoopHole(ScopeGraph graph, Compilation compilation, TypeIndex types) {
            var service = types.Format(LoaderServices.Find(compilation, LoaderServices.EventLoopService));
            if (string.IsNullOrEmpty(service) || HasService(graph, service))
                return;

            var registration = new GraphRegistration {
                Kind = "RegisterInstance",
                ImplementationType = service,
                Lifetime = "Singleton",
                Origin = "InstanceHole",
                Source = "Loader",
                Hole = "builder.Events",
            };
            registration.ServiceTypes.Add(service);
            graph.Registrations.Add(registration);
        }

        private static bool HasService(ScopeGraph graph, string type) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                if (registration.ImplementationType == type)
                    return true;
                for (var s = 0; s < registration.ServiceTypes.Count; s++) {
                    if (registration.ServiceTypes[s] == type)
                        return true;
                }
            }

            return false;
        }

        private static bool HasAnyService(ScopeGraph graph, GraphRegistration candidate) {
            if (HasService(graph, candidate.ImplementationType))
                return true;
            for (var i = 0; i < candidate.ServiceTypes.Count; i++) {
                if (HasService(graph, candidate.ServiceTypes[i]))
                    return true;
            }

            return false;
        }

        private static void IndexParentServices(
            GraphRegistration registration,
            string parentId,
            Dictionary<string, string> exports) {
            if (registration.Origin == "Injectable")
                return;

            var added = false;
            for (var i = 0; i < registration.ServiceTypes.Count; i++) {
                if (string.IsNullOrEmpty(registration.ServiceTypes[i]))
                    continue;
                if (exports.ContainsKey(registration.ServiceTypes[i]) == false)
                    exports.Add(registration.ServiceTypes[i], parentId);
                added = true;
            }

            if (added == false && string.IsNullOrEmpty(registration.ImplementationType) == false) {
                if (exports.ContainsKey(registration.ImplementationType) == false)
                    exports.Add(registration.ImplementationType, parentId);
            }
        }

        private static void Bind(
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
                            graph.Diagnostics.Add(MissingType(registration, registration.Arms[i].ImplementationType));
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
                    graph.Diagnostics.Add(MissingType(registration, "(unbound " + registration.Kind + ")"));
                return;
            }

            var implementation = types.Find(registration.ImplementationType);
            if (implementation == null) {
                graph.Diagnostics.Add(MissingType(registration, registration.ImplementationType));
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
                graph.Diagnostics.Add(TypeProblem(GraphDescriptors.NotConstructible, registration, implementation, constructProblems[i]));
            for (var i = 0; i < injectProblems.Count; i++)
                graph.Diagnostics.Add(TypeProblem(GraphDescriptors.NotInjectable, registration, implementation, injectProblems[i]));
            if (constructProblems.Count > 0 || injectProblems.Count > 0)
                return;

            var model = TypeAnalyzer.Analyze(implementation, references, registration.Location, false);
            if (registration.Origin == "Injectable" && (model == null || string.IsNullOrEmpty(model.ConstructName)))
                graph.Diagnostics.Add(NoInjectMethod(registration));
            if (model == null)
                return;

            WarnUnusedParameters(graph, registration, implementation, model, constructs, types, armParameterType);

            // An [Inject] without parameters gives no edges: the emitter needs the method name separately.
            if (arm < 0)
                registration.InjectMethod = model.ConstructName;

            if (constructs)
                BindParameters(
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

            BindParameters(
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
            var type = PlanFactories.InferDiscriminantType(registration);
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
                graph.Diagnostics.Add(Missing(registration, new ParameterModel(type, "discriminant")));
            }

            registration.Dependencies.Add(edge);
        }

        private static void BindParameters(
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
                    graph.Diagnostics.Add(Missing(registration, parameter));
                }

                registration.Dependencies.Add(edge);
            }
        }

        // At runtime a value of WithParameter belongs to a constructed registration (ContainerBuilder.TryGetParameter).
        // A WithParameter value no parameter can hold goes nowhere: usually a leftover of a removed parameter.
        private static void WarnUnusedParameters(
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

        // The field of an alternative may stay null: the value comes from the last registration of the type that ran.
        // Everything it may come from has to be built before the owner of the edge.
        private static void LinkAlternatives(ScopeGraph graph, Dictionary<string, List<int>> allByType) {
            var groups = BranchGroups.Build(graph.Registrations);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var dependencies = graph.Registrations[i].Dependencies;
                for (var e = 0; e < dependencies.Count; e++) {
                    var edge = dependencies[e];
                    if (edge.Kind != "Registration" || graph.Registrations[edge.TargetIndex].Origin != "Alternative" ||
                        allByType.TryGetValue(edge.ParameterType, out var providers) == false)
                        continue;

                    for (var p = providers.Count - 1; p >= 0; p--) {
                        AddWithGroup(edge, providers[p], groups);
                        if (graph.Registrations[providers[p]].Origin != "Alternative")
                            break;
                    }
                }
            }
        }

        private static void AddWithGroup(GraphEdge edge, int index, List<List<int>> groups) {
            if (edge.Fallbacks.Contains(index) == false)
                edge.Fallbacks.Add(index);
            var group = groups.Find(members => members.Contains(index));
            if (group == null)
                return;

            for (var m = 0; m < group.Count; m++) {
                if (edge.Fallbacks.Contains(group[m]) == false)
                    edge.Fallbacks.Add(group[m]);
            }
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
                Display(graph.Registrations[target]),
                owner.File ?? "",
                owner.Line.ToString(CultureInfo.InvariantCulture)));
        }

        private static readonly string[] PhaseMarkers = {
            "global::Internal.IScopeBaseSetup",
            "global::Internal.IScopeBaseSetupAsync",
            "global::Internal.IScopeSetup",
            "global::Internal.IScopeSetupAsync",
            "global::Internal.IScopeSetupCompletion",
            "global::Internal.IScopeSetupCompletionAsync",
            "global::Internal.IScopeLoaded",
            "global::Internal.IScopeLoadedAsync",
            "global::Internal.IScopeDispose",
            "global::Internal.IScopeDisposeAsync",
        };

        // Every phase resolves its listeners anew: a transient that listens to setup and dispose would get them
        // on two different instances.
        private static void ReportTransientPhaseListeners(ScopeGraph graph) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                if (registration.Lifetime != "Transient")
                    continue;

                var phases = new List<string>();
                for (var s = 0; s < registration.ServiceTypes.Count; s++) {
                    if (Array.IndexOf(PhaseMarkers, registration.ServiceTypes[s]) >= 0)
                        phases.Add(registration.ServiceTypes[s].Substring("global::Internal.".Length));
                }

                if (phases.Count < 2)
                    continue;

                graph.Diagnostics.Add(new DiagnosticInfo(
                    GraphDescriptors.TransientPhaseListener,
                    registration.Location,
                    registration.ImplementationType,
                    string.Join(", ", phases),
                    registration.File ?? "",
                    registration.Line.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static DiagnosticInfo MissingType(GraphRegistration registration, string typeFullName) {
            var line = registration.Line.ToString(CultureInfo.InvariantCulture);
            return new DiagnosticInfo(
                GraphDescriptors.MissingRegistration,
                registration.Location,
                "(implementation)",
                typeFullName,
                string.IsNullOrEmpty(registration.ImplementationType) ? registration.Kind : registration.ImplementationType,
                registration.File ?? "",
                line);
        }

        private static DiagnosticInfo TypeProblem(
            DiagnosticDescriptor descriptor,
            GraphRegistration registration,
            INamedTypeSymbol type,
            string problem) {
            return new DiagnosticInfo(
                descriptor,
                registration.Location,
                TypeNames.ForCode(type),
                problem,
                registration.File ?? "",
                registration.Line.ToString(CultureInfo.InvariantCulture));
        }

        private static DiagnosticInfo NoInjectMethod(GraphRegistration registration) {
            return new DiagnosticInfo(
                GraphDescriptors.InjectableWithoutInject,
                registration.Location,
                registration.ImplementationType,
                registration.File ?? "",
                registration.Line.ToString(CultureInfo.InvariantCulture));
        }

        private static DiagnosticInfo Missing(GraphRegistration registration, ParameterModel parameter) {
            var line = registration.Line.ToString(CultureInfo.InvariantCulture);
            var implementation = string.IsNullOrEmpty(registration.ImplementationType)
                ? registration.Kind
                : registration.ImplementationType;
            return new DiagnosticInfo(
                GraphDescriptors.MissingRegistration,
                registration.Location,
                parameter.Name,
                parameter.TypeFullName,
                implementation,
                registration.File ?? "",
                line);
        }

        private static HashSet<string> ImplicitTypes(Compilation compilation, TypeIndex index) {
            var set = new HashSet<string>(StringComparer.Ordinal);
            AddImplicit(set, index, compilation.GetTypeByMetadataName("Internal.IContainer"));
            AddImplicit(set, index, compilation.GetTypeByMetadataName("Internal.IReadOnlyLifetime"));
            AddImplicit(set, index, compilation.GetTypeByMetadataName("Internal.ILifetime"));
            if (set.Count == 0) {
                set.Add("global::Internal.IContainer");
                set.Add("global::Internal.IReadOnlyLifetime");
                set.Add("global::Internal.ILifetime");
            }

            return set;
        }

        private static void AddImplicit(HashSet<string> set, TypeIndex index, INamedTypeSymbol? type) {
            var formatted = index.Format(type);
            if (string.IsNullOrEmpty(formatted) == false)
                set.Add(formatted);
        }

        private static bool BindsConstructor(string origin) {
            return origin == "ConstructedType" ||
                   origin == "Alternative" ||
                   origin == "ParameterHole" ||
                   origin == "SwitchFactory" ||
                   origin == LoaderServices.Origin;
        }

        private static void DetectCycles(ScopeGraph graph) {
            var count = graph.Registrations.Count;
            var state = new int[count];
            var stack = new List<int>();

            for (var i = 0; i < count; i++) {
                if (state[i] == 0)
                    Visit(i);
            }

            void Visit(int slot) {
                state[slot] = 1;
                stack.Add(slot);

                foreach (var dependency in Neighbors(graph.Registrations[slot])) {
                    if (dependency < 0 || dependency >= count)
                        continue;

                    if (state[dependency] == 1) {
                        graph.Diagnostics.Add(Cycle(graph, stack, dependency));
                        continue;
                    }

                    if (state[dependency] == 0)
                        Visit(dependency);
                }

                stack.RemoveAt(stack.Count - 1);
                state[slot] = 2;
            }
        }

        private static DiagnosticInfo Cycle(ScopeGraph graph, List<int> stack, int start) {
            var startIndex = 0;
            for (var i = 0; i < stack.Count; i++) {
                if (stack[i] == start) {
                    startIndex = i;
                    break;
                }
            }

            var names = new List<string>();
            for (var i = startIndex; i < stack.Count; i++)
                names.Add(Display(graph.Registrations[stack[i]]));
            names.Add(Display(graph.Registrations[start]));

            var path = string.Join(" -> ", names);
            var location = graph.Registrations[start].Location;
            return new DiagnosticInfo(GraphDescriptors.CircularDependency, location, path);
        }

        private static string Display(GraphRegistration registration) {
            if (string.IsNullOrEmpty(registration.ImplementationType) == false)
                return registration.ImplementationType;
            if (registration.Arms.Count > 0 && string.IsNullOrEmpty(registration.Arms[0].ImplementationType) == false)
                return registration.Arms[0].ImplementationType;
            return registration.Kind;
        }

        private static List<int> SortTopologically(ScopeGraph graph) {
            var count = graph.Registrations.Count;
            var order = new List<int>(count);
            var visited = new bool[count];
            var groups = BranchGroups.Build(graph.Registrations);

            for (var i = 0; i < count; i++)
                Visit(i);

            return order;

            // The members of a group are built together, by one field: every dependency of each of them goes first.
            void Visit(int slot) {
                if (visited[slot])
                    return;

                var members = groups.Find(group => group.Contains(slot)) ?? new List<int> { slot };
                for (var m = 0; m < members.Count; m++)
                    visited[members[m]] = true;

                for (var m = 0; m < members.Count; m++) {
                    foreach (var dependency in Neighbors(graph.Registrations[members[m]])) {
                        if (dependency >= 0 && dependency < count && dependency != members[m])
                            Visit(dependency);
                    }
                }

                order.AddRange(members);
            }
        }

        private static IEnumerable<int> Neighbors(GraphRegistration registration) {
            for (var i = 0; i < registration.Dependencies.Count; i++) {
                var edge = registration.Dependencies[i];
                if (edge.Kind == "Collection") {
                    for (var c = 0; c < edge.CollectionIndices.Count; c++)
                        yield return edge.CollectionIndices[c];
                    continue;
                }

                if (edge.Kind == "Registration") {
                    yield return edge.TargetIndex;
                    for (var f = 0; f < edge.Fallbacks.Count; f++)
                        yield return edge.Fallbacks[f];
                }
            }
        }

        private readonly struct Step {
            public readonly int Ordinal;
            public readonly int Sequence;
            public readonly GraphRegistration? Registration;
            public readonly string? CallId;
            public readonly int CallIndex;

            public Step(int ordinal, int sequence, GraphRegistration? registration, string? callId, int callIndex) {
                Ordinal = ordinal;
                Sequence = sequence;
                Registration = registration;
                CallId = callId;
                CallIndex = callIndex;
            }
        }
    }
}
