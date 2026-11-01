using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class GraphDocument {
        public string AssemblyName = "";
        public List<GraphMethod> Methods = new List<GraphMethod>();
        public List<DiagnosticInfo> Diagnostics = new List<DiagnosticInfo>();
    }
}
