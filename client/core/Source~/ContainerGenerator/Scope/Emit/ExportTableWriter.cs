using System.Collections.Generic;
using System.Linq;

namespace ContainerGenerator {
    internal static class ExportTableWriter {
        public static void WriteFields(CodeWriter writer, ScopePlan plan, string indent) {
            for (var i = 0; i < plan.Fields.Count; i++) {
                var field = plan.Fields[i];
                writer.AppendLine(indent + "private readonly " + field.Type + " " + field.Name + ";");
            }

            WriteExportTable(writer, plan, indent);
            if (plan.HasContainerDiagnostics)
                writer.AppendLine(indent + "private readonly global::Internal.ContainerDiagnostics _diagnostics;");
            writer.AppendLine(indent + "private bool _isDisposing;");
            writer.AppendLine(indent + "private bool _disposed;");
        }

        // Type -> the number of the instance in GetExport. The table is static, shared by every instance of the class:
        // the instances are in the fields anyway, and a scope does not pay for a dictionary of its own (340-790 bytes).
        private static void WriteExportTable(CodeWriter writer, ScopePlan plan, string indent) {
            var values = ExportValues(plan);
            writer.AppendLine(indent + "private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> _exports =");
            writer.AppendLine(indent + "    new global::System.Collections.Generic.Dictionary<global::System.Type, int>(" +
                              plan.Exports.Count.ToString() + ") {");
            for (var i = 0; i < plan.Exports.Count; i++) {
                var export = plan.Exports[i];
                var index = values.IndexOf(ExportValue(export));
                var comma = i + 1 < plan.Exports.Count ? "," : "";
                writer.AppendLine(indent + "        { typeof(" + export.ServiceType + "), " + index.ToString() + " }" + comma);
            }

            writer.AppendLine(indent + "    };");
        }

        public static void WriteGetExport(CodeWriter writer, ScopePlan plan, string indent, string body) {
            var values = ExportValues(plan);
            writer.AppendLine(indent + "private object GetExport(int index) {");
            writer.AppendLine(body + "switch (index) {");
            for (var i = 0; i < values.Count; i++)
                writer.AppendLine(body + "    case " + i.ToString() + ": return " + values[i] + ";");
            writer.AppendLine(body + "    default: return null;");
            writer.AppendLine(body + "}");
            writer.AppendLine(indent + "}");
            writer.AppendLine();
        }

        // Several service types of one instance share one case.
        private static List<string> ExportValues(ScopePlan plan) {
            var values = new List<string>();
            for (var i = 0; i < plan.Exports.Count; i++) {
                var value = ExportValue(plan.Exports[i]);
                if (values.Contains(value) == false)
                    values.Add(value);
            }

            return values;
        }

        private static string ExportValue(ScopePlan.Export export) {
            return string.IsNullOrEmpty(export.Value) ? "this" : export.Value;
        }
    }
}
