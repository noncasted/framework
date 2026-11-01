using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class GraphEdge {
        public string ParameterName = "";
        public string ParameterType = "";
        public string Source = "Constructor";
        // The name of the [Inject] method for the edges with Source == "Construct".
        public string Method = "";
        public string Kind = "Registration";
        public int Arm = -1;
        public int TargetIndex = -1;
        public string ParentRootId = "";
        public List<int> CollectionIndices = new List<int>();
        // A target that is an alternative may stay null: what the value may come from instead (the other members of
        // its group, the earlier registrations of the type). They are built before the owner of the edge.
        public List<int> Fallbacks = new List<int>();
    }
}
