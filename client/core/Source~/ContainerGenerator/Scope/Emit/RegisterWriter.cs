using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class RegisterWriter {
        public static void WriteRegister(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            string indent,
            string body) {
            if (plan.HasRuntimeInitialize)
                writer.AppendLine(indent + "[global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]");

            writer.AppendLine(indent + "private static void RegisterGenerated() {");
            if (string.IsNullOrEmpty(plan.ViewType) == false && plan.OriginRootId != plan.RootId) {
                // An entity variant: the loader finds the class by the root and the concrete view type.
                writer.AppendLine(body + "global::Internal.GeneratedScopes.RegisterVariant(");
                writer.AppendLine(body + "    " + EmitLiterals.Literal(plan.OriginRootId) + ",");
                writer.AppendLine(body + "    typeof(global::" + ScopeNames.StripGlobal(plan.ViewType) + "),");
            }
            else {
                writer.AppendLine(body + "global::Internal.GeneratedScopes.Register(");
                writer.AppendLine(body + "    " + EmitLiterals.Literal(plan.RootId) + ",");
            }

            writer.AppendLine(body + "    request => new " + plan.ClassName + "(");
            for (var i = 0; i < plan.CtorParams.Count; i++) {
                var param = plan.CtorParams[i];
                var argument = param.IsLifetime
                    ? "request.Lifetime"
                    : param.IsParent
                        ? "request.Parent"
                        : param.IsRequest
                            ? "request"
                            : param.IsInjections
                                ? "request.GetInjections<" + param.ElementType + ">()"
                                : param.IsInstance
                                    ? "request.GetInstance<" + param.Type + ">(" + param.Occurrence.ToString() + ")"
                                    : param.IsParameter
                                        ? ParameterArgument(param)
                                        : param.IsParentHole
                                            ? "request.GetFromParent<" + param.Type + ">()"
                                            : "request.Get<" + param.Type + ">()";
                var comma = i + 1 < plan.CtorParams.Count ? "," : "";
                writer.AppendLine(body + "        " + argument + comma);
            }

            writer.AppendLine(body + "    ),");
            writer.AppendLine(body + "    " + EmitLiterals.TypeArray(ShapeConstructed(graph)) + ",");
            writer.AppendLine(body + "    " + EmitLiterals.TypeArray(ShapeInstances(graph)) + ");");
            writer.AppendLine(indent + "}");
            writer.AppendLine();
        }

        private static string ParameterArgument(ScopePlan.CtorParam param) {
            var value = "request.GetParameter<" + param.Type + ">(typeof(" + param.ParameterImplementation + "), " + param.Occurrence.ToString() + ")";
            return param.IsAlternativeParameter
                ? "request.IsRegistered(typeof(" + param.ParameterImplementation + ")) ? " + value + " : default(" + param.Type + ")"
                : value;
        }

        // What the installer registers at runtime, for the check of the builder against the class: the implementation of
        // every Register (each member of an alternative, each arm of a switch) and the declared type of every instance.
        private static List<string> ShapeConstructed(ScopeGraph graph) {
            var types = new List<string>();
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                if (registration.Origin == "SwitchFactory") {
                    for (var a = 0; a < registration.Arms.Count; a++)
                        AddType(types, registration.Arms[a].ImplementationType);
                    continue;
                }

                if (RegistrationOccurrences.IsRuntimeRegister(registration))
                    AddType(types, registration.ImplementationType);
            }

            return types;
        }

        private static List<string> ShapeInstances(ScopeGraph graph) {
            var types = new List<string>();
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                if (RegistrationOccurrences.IsInstanceKind(registration.Kind) && string.IsNullOrEmpty(registration.ImplementationType) == false)
                    types.Add(registration.ImplementationType);
            }

            return types;
        }

        private static void AddType(List<string> types, string type) {
            if (string.IsNullOrEmpty(type) == false && types.Contains(type) == false)
                types.Add(type);
        }
    }
}
