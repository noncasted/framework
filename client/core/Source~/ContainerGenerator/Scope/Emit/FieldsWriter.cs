namespace ContainerGenerator {
    internal static class FieldsWriter {
        public static void WriteFields(CodeWriter writer, ScopePlan plan, string indent) {
            for (var i = 0; i < plan.Fields.Count; i++) {
                var field = plan.Fields[i];
                writer.AppendLine(indent + "private readonly " + field.Type + " " + field.Name + ";");
            }

            ExportTableWriter.WriteExportTable(writer, plan, indent);
            if (plan.HasContainerDiagnostics)
                writer.AppendLine(indent + "private readonly global::Internal.ContainerDiagnostics _diagnostics;");
            writer.AppendLine(indent + "private bool _isDisposing;");
            writer.AppendLine(indent + "private bool _disposed;");
        }
    }
}
