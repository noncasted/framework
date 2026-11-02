using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class RegistrationFlattener {
        public static void Flatten(
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
                        if (string.IsNullOrEmpty(clone.BranchPath) == false)
                            clone.PrefixBranchPath(walk);
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
                    if (string.IsNullOrEmpty(hole) == false)
                        returned.AddHole(hole);
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
