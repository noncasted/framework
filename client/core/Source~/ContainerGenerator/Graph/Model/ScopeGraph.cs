using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class ScopeGraph {
        public string RootId { get; init; } = "";
        public string ParentId { get; init; } = "";
        public bool ParentMissing { get; private set; }
        public string Variant { get; init; } = "";
        public string ViewType { get; init; } = "";
        public List<GraphRegistration> Registrations { get; } = new List<GraphRegistration>();
        public List<int> ConstructionOrder { get; } = new List<int>();
        public List<DiagnosticInfo> Diagnostics { get; } = new List<DiagnosticInfo>();

        public void MarkParentMissing() {
            ParentMissing = true;
        }
    }
}
