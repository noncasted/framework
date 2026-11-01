using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class DiagnosticsWriter {
        // The table is static: an instance of the scope pays only for the ContainerDiagnostics object.
        public static void WriteDiagnostics(CodeWriter writer, ScopePlan plan, ScopeGraph graph, string indent, string body) {
            if (plan.HasContainerDiagnostics == false)
                return;

            writer.AppendLine("#if UNITY_EDITOR || DEBUG");
            writer.AppendLine(indent + "private static readonly global::Internal.RegistrationInfo[] _registrationInfos = {");
            for (var i = 0; i < plan.DiagnosticRegistrations.Count; i++) {
                var entry = plan.DiagnosticRegistrations[i];
                var comma = i + 1 < plan.DiagnosticRegistrations.Count ? "," : "";
                var implementation = string.IsNullOrEmpty(entry.ImplementationType)
                    ? "null"
                    : "typeof(" + entry.ImplementationType + ")";
                writer.AppendLine(body + "new global::Internal.RegistrationInfo(" +
                                  i.ToString() + ", " +
                                  implementation + ", " +
                                  EmitLiterals.TypeArray(entry.ServiceTypes) + ", " +
                                  "global::Internal.ServiceLifetime." + LifetimeMember(entry.Lifetime) + ", " +
                                  IntArray(entry.Dependencies) + ", " +
                                  (entry.IsInstantiated ? "true" : "false") + ", " +
                                  (entry.IsExternal ? "true" : "false") + ")" + comma);
            }

            writer.AppendLine(indent + "};");
            writer.AppendLine();
            writer.AppendLine(indent + "private static readonly int[] _buildOrder = " + IntArray(graph.ConstructionOrder) + ";");
            writer.AppendLine("#endif");
        }

        private static string IntArray(List<int> values) {
            if (values.Count == 0)
                return "global::System.Array.Empty<int>()";

            var items = new List<string>();
            for (var i = 0; i < values.Count; i++)
                items.Add(values[i].ToString());
            return "new int[] { " + string.Join(", ", items) + " }";
        }

        private static string LifetimeMember(string lifetime) {
            return lifetime == "Transient" || lifetime == "Scoped" ? lifetime : "Singleton";
        }
    }
}
