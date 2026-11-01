using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class ConstructorWriter {
        public static void WriteConstructor(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            string indent,
            string body) {
            writer.AppendLine(indent + "public " + plan.ClassName + "(");
            for (var i = 0; i < plan.CtorParams.Count; i++) {
                var param = plan.CtorParams[i];
                var comma = i + 1 < plan.CtorParams.Count ? "," : "";
                writer.AppendLine(body + param.Type + " " + param.Name + comma);
            }

            writer.AppendLine(indent + ") {");

            for (var i = 0; i < plan.CtorParams.Count; i++) {
                var param = plan.CtorParams[i];
                if (param.IsLifetime)
                    writer.AppendLine(body + plan.LifetimeField + " = " + param.Name + ";");
                else if (param.IsParent)
                    writer.AppendLine(body + plan.ParentField + " = " + param.Name + ";");
                else if (param.IsAlternative || param.IsRequest)
                    continue;
                else if (string.IsNullOrEmpty(param.FieldName) == false)
                    writer.AppendLine(body + param.FieldName + " = " + param.Name + ";");
                else if (param.Slot >= 0 && param.Slot < plan.Slots.Count) {
                    var slot = plan.Slots[param.Slot];
                    if (slot.IsHole && string.IsNullOrEmpty(slot.FieldName) == false)
                        writer.AppendLine(body + slot.FieldName + " = " + param.Name + ";");
                }
            }

            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (string.IsNullOrEmpty(slot.RegisteredField) == false)
                    writer.AppendLine(body + slot.RegisteredField + " = request.IsRegistered(typeof(" + slot.ImplementationType + "));");
            }

            var constructed = new HashSet<int>();
            for (var o = 0; o < graph.ConstructionOrder.Count; o++) {
                var index = graph.ConstructionOrder[o];
                if (index < 0 || index >= plan.Slots.Count)
                    continue;
                WriteSlotConstruction(writer, plan, graph, index, body, constructed);
            }

            for (var i = 0; i < plan.Slots.Count; i++) {
                if (constructed.Contains(i) == false)
                    WriteSlotConstruction(writer, plan, graph, i, body, constructed);
            }

            MarkerWriter.WriteMarkers(writer, plan, graph, body);
            if (plan.HasContainerDiagnostics) {
                // Diagnostics exist only in the editor and a dev build; the benchmark turns them off with the flag.
                writer.AppendLine("#if UNITY_EDITOR || DEBUG");
                writer.AppendLine(body + "if (global::Internal.ContainerRegistryDebug.IsEnabled == true) {");
                writer.AppendLine(body + "    _diagnostics = new global::Internal.ContainerDiagnostics(");
                writer.AppendLine(body + "        " + EmitLiterals.Literal(plan.ClassName) + ",");
                writer.AppendLine(body + "        " + plan.ParentField + " != null ? " + plan.ParentField + ".Diagnostics : null,");
                writer.AppendLine(body + "        _registrationInfos,");
                writer.AppendLine(body + "        _buildOrder);");
                writer.AppendLine(body + "}");
                writer.AppendLine("#endif");
            }

            writer.AppendLine(indent + "}");
        }

        private static void WriteSlotConstruction(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            int index,
            string indent,
            HashSet<int> constructed) {
            if (constructed.Add(index) == false)
                return;

            var slot = plan.Slots[index];
            if (slot.SkipEmit || slot.IsTransient)
                return;
            if (slot.IsSwitch) {
                WriteSwitchConstruction(writer, plan, graph, slot, indent);
                return;
            }
            if (slot.IsAlternative && slot.IsGroupPrimary == false)
                return;

            if (slot.IsHole && slot.IsInjectionsRepeat)
                return;

            if (slot.IsHole && string.IsNullOrEmpty(slot.InjectionsParamName) == false) {
                var injections = slot.InjectionsParamName;
                var counter = slot.InjectionsIndexName;
                writer.AppendLine(indent + "for (var " + counter + " = 0; " + counter + " < " + injections + ".Length; " + counter + "++) {");
                WriteConstructCall(writer, plan, graph, slot, injections + "[" + counter + "]", indent + "    ");
                writer.AppendLine(indent + "}");
                return;
            }

            if (slot.IsHole) {
                WriteConstructCall(writer, plan, graph, slot, slot.FieldName, indent);
                return;
            }

            if (slot.IsAlternative && slot.IsGroupPrimary) {
                WriteAlternative(writer, plan, graph, slot, indent);
                return;
            }

            if (string.IsNullOrEmpty(slot.FieldName) || string.IsNullOrEmpty(slot.ImplementationType))
                return;

            writer.AppendLine(indent + slot.FieldName + " = " +
                              ArgumentExpressions.NewExpression(plan, graph, slot.ImplementationType, slot.Registration, "Constructor") + ";");
            WriteConstructCall(writer, plan, graph, slot, slot.FieldName, indent);
        }

        // A singleton of a switch arm: the field built by the value of the discriminant in the scope.
        private static void WriteSwitchConstruction(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            ScopePlan.Slot slot,
            string indent) {
            GraphEdge? discriminant = null;
            for (var i = 0; i < slot.Registration.Dependencies.Count; i++) {
                if (slot.Registration.Dependencies[i].Source == "Discriminant")
                    discriminant = slot.Registration.Dependencies[i];
            }

            if (discriminant == null || string.IsNullOrEmpty(slot.FieldName))
                throw new InvalidOperationException("Generator switch '" + slot.CreateMethod + "' has no discriminant.");

            writer.AppendLine(indent + slot.FieldName + " = " + slot.CreateMethod + "(" +
                              ArgumentExpressions.Argument(plan, graph, discriminant) +
                              (plan.NeedsRequest ? ", request" : "") + ");");
        }

        private static void WriteAlternative(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            ScopePlan.Slot slot,
            string indent) {
            // One if / switch branch of the installer ran: the implementation that got into the builder is built, with
            // its own [Inject]. An if without else registers nothing when its branch does not run: the field stays
            // null, the scope still builds, and only what depends on it fails (RequireAlternative).
            for (var m = 0; m < slot.GroupMembers.Count; m++) {
                var member = plan.Slots[slot.GroupMembers[m]];
                writer.AppendLine(indent + (m == 0 ? "" : "else ") + "if (request.IsRegistered(typeof(" + member.ImplementationType + "))) {");
                writer.AppendLine(indent + "    var alternative = " +
                                  ArgumentExpressions.NewExpression(plan, graph, member.ImplementationType, member.Registration, "Constructor") + ";");
                WriteConstructCall(writer, plan, graph, member, "alternative", indent + "    ");
                writer.AppendLine(indent + "    " + slot.FieldName + " = alternative;");
                writer.AppendLine(indent + "}");
            }
        }

        private static void WriteConstructCall(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            ScopePlan.Slot slot,
            string target,
            string indent) {
            if (string.IsNullOrEmpty(target))
                return;

            var args = ArgumentExpressions.Arguments(plan, graph, slot.Registration, "Construct");
            if (args == null)
                return;
            var method = ConstructName(slot.Registration);
            if (args.Count == 0)
                writer.AppendLine(indent + target + "." + method + "();");
            else
                writer.AppendLine(indent + target + "." + method + "(" + string.Join(", ", args) + ");");
        }

        // The name of the [Inject] method is on the edges with Source == "Construct"; without edges there is no call.
        public static string ConstructName(GraphRegistration registration, int arm = -1) {
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
    }
}
