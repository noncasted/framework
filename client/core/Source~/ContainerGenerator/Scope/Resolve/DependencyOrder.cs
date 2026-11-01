using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class DependencyOrder {
        public static void DetectCycles(ScopeGraph graph) {
            var count = graph.Registrations.Count;
            var state = new int[count];
            var stack = new List<int>();

            for (var i = 0; i < count; i++) {
                if (state[i] == 0)
                    Visit(i);
            }

            void Visit(int slot) {
                state[slot] = 1;
                stack.Add(slot);

                foreach (var dependency in Neighbors(graph.Registrations[slot])) {
                    if (dependency < 0 || dependency >= count)
                        continue;

                    if (state[dependency] == 1) {
                        graph.Diagnostics.Add(Cycle(graph, stack, dependency));
                        continue;
                    }

                    if (state[dependency] == 0)
                        Visit(dependency);
                }

                stack.RemoveAt(stack.Count - 1);
                state[slot] = 2;
            }
        }

        private static DiagnosticInfo Cycle(ScopeGraph graph, List<int> stack, int start) {
            var startIndex = 0;
            for (var i = 0; i < stack.Count; i++) {
                if (stack[i] == start) {
                    startIndex = i;
                    break;
                }
            }

            var names = new List<string>();
            for (var i = startIndex; i < stack.Count; i++)
                names.Add(ResolveDiagnostics.Display(graph.Registrations[stack[i]]));
            names.Add(ResolveDiagnostics.Display(graph.Registrations[start]));

            var path = string.Join(" -> ", names);
            var location = graph.Registrations[start].Location;
            return new DiagnosticInfo(GraphDescriptors.CircularDependency, location, path);
        }

        public static List<int> SortTopologically(ScopeGraph graph) {
            var count = graph.Registrations.Count;
            var order = new List<int>(count);
            var visited = new bool[count];
            var groups = BranchGroups.Build(graph.Registrations);

            for (var i = 0; i < count; i++)
                Visit(i);

            return order;

            // The members of a group are built together, by one field: every dependency of each of them goes first.
            void Visit(int slot) {
                if (visited[slot])
                    return;

                var members = groups.Find(group => group.Contains(slot)) ?? new List<int> { slot };
                for (var m = 0; m < members.Count; m++)
                    visited[members[m]] = true;

                for (var m = 0; m < members.Count; m++) {
                    foreach (var dependency in Neighbors(graph.Registrations[members[m]])) {
                        if (dependency >= 0 && dependency < count && dependency != members[m])
                            Visit(dependency);
                    }
                }

                order.AddRange(members);
            }
        }

        private static IEnumerable<int> Neighbors(GraphRegistration registration) {
            for (var i = 0; i < registration.Dependencies.Count; i++) {
                var edge = registration.Dependencies[i];
                if (edge.Kind == "Collection") {
                    for (var c = 0; c < edge.CollectionIndices.Count; c++)
                        yield return edge.CollectionIndices[c];
                    continue;
                }

                if (edge.Kind == "Registration") {
                    yield return edge.TargetIndex;
                    for (var f = 0; f < edge.Fallbacks.Count; f++)
                        yield return edge.Fallbacks[f];
                }
            }
        }
    }
}
