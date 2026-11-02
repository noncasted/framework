namespace ContainerGenerator {
    internal static class FactoryWriter {
        public static void WriteFactories(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            string indent,
            string body) {
            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.IsTransient == false || string.IsNullOrEmpty(slot.CreateMethod))
                    continue;

                writer.AppendLine(indent + "private " + slot.ImplementationType + " " + slot.CreateMethod + "() {");
                var created = ArgumentExpressions.NewExpression(plan, graph, slot.ImplementationType, slot.Registration, "Constructor");
                var construct = ArgumentExpressions.Arguments(plan, graph, slot.Registration, "Construct");
                if (construct == null) {
                    writer.AppendLine(body + "return " + created + ";");
                }
                else {
                    writer.AppendLine(body + "var instance = " + created + ";");
                    writer.AppendLine(body + ArgumentExpressions.ConstructCall(slot.Registration, "instance", construct) + ";");
                    writer.AppendLine(body + "return instance;");
                }

                writer.AppendLine(indent + "}");
                writer.AppendLine();
            }

            for (var i = 0; i < plan.Factories.Count; i++) {
                var factory = plan.Factories[i];
                var slot = plan.Slots[factory.Slot];
                writer.AppendLine(indent + "private " + factory.ReturnType + " " + factory.MethodName + "(" +
                                  factory.DiscriminantType + " " + factory.DiscriminantName +
                                  (plan.NeedsRequest ? ", global::Internal.GeneratedScopeRequest request" : "") + ") {");
                writer.AppendLine(body + "switch (" + factory.DiscriminantName + ") {");
                for (var a = 0; a < slot.Registration.Arms.Count; a++) {
                    var arm = slot.Registration.Arms[a];
                    var discriminant = string.IsNullOrEmpty(arm.Discriminant) ? "default" : arm.Discriminant;
                    var created = ArgumentExpressions.NewExpression(plan, graph, arm.ImplementationType, slot.Registration, "Constructor", a);
                    var construct = ArgumentExpressions.Arguments(plan, graph, slot.Registration, "Construct", a);
                    if (construct == null) {
                        writer.AppendLine(body + "    case " + discriminant + ": return " + created + ";");
                        continue;
                    }

                    writer.AppendLine(body + "    case " + discriminant + ": {");
                    writer.AppendLine(body + "        var instance = " + created + ";");
                    writer.AppendLine(body + "        " + ArgumentExpressions.ConstructCall(slot.Registration, "instance", construct, a) + ";");
                    writer.AppendLine(body + "        return instance;");
                    writer.AppendLine(body + "    }");
                }

                writer.AppendLine(body + "    default: throw new global::System.ArgumentOutOfRangeException(nameof(" +
                                  factory.DiscriminantName + "), " + factory.DiscriminantName + ", null);");
                writer.AppendLine(body + "}");
                writer.AppendLine(indent + "}");
                writer.AppendLine();
            }
        }

        public static void WriteProvides(
            CodeWriter writer,
            ScopePlan plan,
            ScopeGraph graph,
            string indent,
            string body) {
            for (var i = 0; i < plan.Provides.Count; i++) {
                var provide = plan.Provides[i];
                var slot = plan.Slots[provide.Slot];
                writer.AppendLine(indent + "void global::Internal.IProvides<" + provide.TargetType + ">.Construct(" +
                                  provide.TargetType + " target) {");
                // PlanExports gives a Provide only to a slot with Construct edges: the arguments are there.
                var args = ArgumentExpressions.Arguments(plan, graph, slot.Registration, "Construct")!;
                writer.AppendLine(body + ArgumentExpressions.ConstructCall(slot.Registration, "target", args) + ";");
                writer.AppendLine(indent + "}");
                writer.AppendLine();
            }
        }
    }
}
