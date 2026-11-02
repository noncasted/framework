using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class GraphDocument {
        public string AssemblyName { get; init; } = "";
        public List<GraphMethod> Methods { get; } = new List<GraphMethod>();
        public List<DiagnosticInfo> Diagnostics { get; } = new List<DiagnosticInfo>();
    }
}
