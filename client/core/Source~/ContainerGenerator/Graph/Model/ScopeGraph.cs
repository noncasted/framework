using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class ScopeGraph {
        public string RootId = "";
        public string ParentId = "";
        public bool ParentMissing;
        public string Variant = "";
        public string ViewType = "";
        public List<GraphRegistration> Registrations = new List<GraphRegistration>();
        public List<int> ConstructionOrder = new List<int>();
        public List<DiagnosticInfo> Diagnostics = new List<DiagnosticInfo>();
    }
}
