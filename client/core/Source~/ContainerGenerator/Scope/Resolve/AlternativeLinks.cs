using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class AlternativeLinks {
        // The field of an alternative may stay null: the value comes from the last registration of the type that ran.
        // Everything it may come from has to be built before the owner of the edge.
        public static void LinkAlternatives(ScopeGraph graph, Dictionary<string, List<int>> allByType) {
            var groups = BranchGroups.Build(graph.Registrations);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var dependencies = graph.Registrations[i].Dependencies;
                for (var e = 0; e < dependencies.Count; e++) {
                    var edge = dependencies[e];
                    if (edge.Kind != "Registration" || graph.Registrations[edge.TargetIndex].Origin != "Alternative" ||
                        allByType.TryGetValue(edge.ParameterType, out var providers) == false)
                        continue;

                    for (var p = providers.Count - 1; p >= 0; p--) {
                        AddWithGroup(edge, providers[p], groups);
                        if (graph.Registrations[providers[p]].Origin != "Alternative")
                            break;
                    }
                }
            }
        }

        private static void AddWithGroup(GraphEdge edge, int index, List<List<int>> groups) {
            if (edge.Fallbacks.Contains(index) == false)
                edge.Fallbacks.Add(index);
            var group = groups.Find(members => members.Contains(index));
            if (group == null)
                return;

            for (var m = 0; m < group.Count; m++) {
                if (edge.Fallbacks.Contains(group[m]) == false)
                    edge.Fallbacks.Add(group[m]);
            }
        }
    }
}
