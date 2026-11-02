using System;
using System.Collections.Generic;

namespace Internal
{
    // The plan of a prefab batch as it is written to SessionState: the names of the fields are its format.
    [Serializable]
    internal sealed class HierarchyBindingsPrefabPlan
    {
        public List<string> Assets = new();
        public List<Phase> Phases = new();
        public int Next;

        [Serializable]
        public sealed class Phase
        {
            public List<HierarchyBindingsRequest> Requests = new();
        }
    }
}
