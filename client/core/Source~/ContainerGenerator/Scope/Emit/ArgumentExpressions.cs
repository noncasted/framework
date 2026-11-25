using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class ArgumentExpressions {
        public static string NewExpression(
            ScopePlan plan,
            ScopeGraph graph,
            string type,
            GraphRegistration registration,
            string source,
            int arm = -1) {
            var args = Arguments(plan, graph, registration, source, arm);
            if (args == null || args.Count == 0)
                return "new " + type + "()";
            return "new " + type + "(" + string.Join(", ", args) + ")";
        }

        public static List<string>? Arguments(
            ScopePlan plan,
            ScopeGraph graph,
            GraphRegistration registration,
            string source,
            int arm = -1) {
            var found = false;
            var args = new List<string>();
            for (var i = 0; i < registration.Dependencies.Count; i++) {
                var edge = registration.Dependencies[i];
                if (edge.Source != source)
                    continue;
                if (edge.Arm != arm)
                    continue;
                found = true;
                args.Add(Argument(plan, graph, edge));
            }

            // An [Inject] without parameters gives no edges, but is called.
            if (found == false)
                return source == "Construct" && (arm >= 0 || string.IsNullOrEmpty(registration.InjectMethod)) ? null : args;
            return args;
        }

        public static string ConstructCall(GraphRegistration registration, string target, List<string> args, int arm = -1) {
            return target + "." + ConstructName(registration, arm) + "(" + string.Join(", ", args) + ")";
        }

        // The name of the [Inject] method is on the edges with Source == "Construct"; without edges there is no call.
        private static string ConstructName(GraphRegistration registration, int arm) {
            for (var i = 0; i < registration.Dependencies.Count; i++) {
                var edge = registration.Dependencies[i];
                if (edge.Source != "Construct" || edge.Arm != arm)
                    continue;
                if (string.IsNullOrEmpty(edge.Method) == false)
                    return edge.Method;
            }

            if (arm < 0 && string.IsNullOrEmpty(registration.InjectMethod) == false)
                return registration.InjectMethod;
            return "Construct";
        }

        public static string Argument(ScopePlan plan, ScopeGraph graph, GraphEdge edge) {
            if (edge.Kind == "Implicit") {
                if (IsContainerType(edge.ParameterType))
                    return "this";
                return plan.LifetimeField;
            }

            if (edge.Kind == "Log" && LogTypes.TryArgument(edge.ParameterType, out var logged))
                return LogTypes.Expression(logged);

            if (edge.Kind == "ArmParameter")
                return ArmParameter(graph, edge);

            if (edge.Kind == "Hole")
                return HoleParam(plan, edge);

            if (edge.Kind == "Collection")
                return CollectionExpression(plan, edge);

            if (edge.Kind == "Registration" && edge.TargetIndex >= 0 && edge.TargetIndex < plan.Slots.Count) {
                var target = plan.Slots[edge.TargetIndex];
                if (target.IsAlternative)
                    return AlternativeExpressions.Required(plan, edge.ParameterType, EmitLiterals.Literal(ScopeNames.StripGlobal(edge.ParameterType)));
                if (target.IsTransient && string.IsNullOrEmpty(target.CreateMethod) == false)
                    return target.CreateMethod + "()";
                if (string.IsNullOrEmpty(target.FieldName) == false)
                    return target.FieldName;
            }

            throw new InvalidOperationException(
                "Generator cannot emit argument for '" + edge.ParameterName + "' of type '" + edge.ParameterType + "'.");
        }

        // The value of the registration of the chosen arm: by type only it would be the last registration of the type.
        private static string ArmParameter(ScopeGraph graph, GraphEdge edge) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                if (registration.Dependencies.Contains(edge) == false)
                    continue;

                var implementation = registration.Arms[edge.Arm].ImplementationType;
                return "request.GetParameter<" + edge.ParameterType + ">(typeof(" + implementation + "), " +
                       RegistrationOccurrences.ArmOccurrence(graph, i, implementation).ToString() + ")";
            }

            throw new InvalidOperationException(
                "Generator parameter '" + edge.ParameterName + "' of a switch arm has no registration.");
        }

        private static string CollectionExpression(ScopePlan plan, GraphEdge edge) {
            if (CollectionTypes.TryElement(edge.ParameterType, out var element) == false)
                throw new InvalidOperationException("Generator has no list for the type '" + edge.ParameterType + "'.");

            var items = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var hasAlternative = false;
            for (var i = 0; i < edge.CollectionIndices.Count; i++) {
                var index = edge.CollectionIndices[i];
                if (index < 0 || index >= plan.Slots.Count)
                    continue;
                var slot = plan.Slots[index];
                if (slot.IsTransient && string.IsNullOrEmpty(slot.CreateMethod) == false) {
                    items.Add(AlternativeExpressions.Item(slot, element));
                    hasAlternative |= slot.IsAlternative;
                }
                else if (string.IsNullOrEmpty(slot.FieldName) == false && (slot.IsAlternative == false || seen.Add(slot.FieldName))) {
                    items.Add(AlternativeExpressions.Item(slot, element));
                    hasAlternative |= slot.IsAlternative;
                }
            }

            return AlternativeExpressions.WithoutAbsent("new " + element + "[] { " + string.Join(", ", items) + " }", hasAlternative);
        }

        private static string HoleParam(ScopePlan plan, GraphEdge edge) {
            for (var i = 0; i < plan.CtorParams.Count; i++) {
                if (plan.CtorParams[i].Edges.Contains(edge))
                    return HoleValue(plan, plan.CtorParams[i]);
            }

            throw new InvalidOperationException(
                "Generator hole '" + edge.ParameterName + "' of type '" + edge.ParameterType +
                "' has no constructor parameter.");
        }

        // The value of a hole is available everywhere in the class: the field of the edge or of the slot, not the ctor parameter.
        private static string HoleValue(ScopePlan plan, ScopePlan.CtorParam param) {
            if (string.IsNullOrEmpty(param.FieldName) == false)
                return param.FieldName;
            if (param.Slot >= 0 && param.Slot < plan.Slots.Count &&
                string.IsNullOrEmpty(plan.Slots[param.Slot].FieldName) == false)
                return plan.Slots[param.Slot].FieldName;
            return param.Name;
        }

        private static bool IsContainerType(string type) {
            return type == "global::Internal.IContainer" || type == "Internal.IContainer";
        }
    }
}
