using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class GraphEdge {
        public string ParameterName { get; init; } = "";
        public string ParameterType { get; init; } = "";
        public string Source { get; init; } = "Constructor";
        // The name of the [Inject] method for the edges with Source == "Construct".
        public string Method { get; init; } = "";
        public string Kind { get; private set; } = "Registration";
        public int Arm { get; init; } = -1;
        public int TargetIndex { get; private set; } = -1;
        public string ParentRootId { get; private set; } = "";
        public List<int> CollectionIndices { get; } = new List<int>();
        // A target that is an alternative may stay null: what the value may come from instead (the other members of
        // its group, the earlier registrations of the type). They are built before the owner of the edge.
        public List<int> Fallbacks { get; } = new List<int>();

        public void Bind(string kind) {
            Kind = kind;
        }

        public void BindRegistration(int target) {
            Kind = "Registration";
            TargetIndex = target;
        }

        public void BindCollection(List<int> indices) {
            Kind = "Collection";
            CollectionIndices.AddRange(indices);
            if (indices.Count > 0)
                TargetIndex = indices[indices.Count - 1];
        }

        public void BindParent(string parentRootId) {
            Kind = "Hole";
            ParentRootId = parentRootId;
        }
    }
}
