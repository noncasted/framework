using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class PlanMarkers {
        public static void BuildMarkers(ScopePlan plan, ScopeGraph graph, HashSet<string> used) {
            var byType = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                var slot = plan.Slots[i];
                if (slot.SkipEmit)
                    continue;

                var added = false;
                for (var s = 0; s < registration.ServiceTypes.Count; s++) {
                    AddMarker(byType, registration.ServiceTypes[s], i);
                    added = true;
                }

                if (added == false && string.IsNullOrEmpty(registration.ImplementationType) == false)
                    AddMarker(byType, registration.ImplementationType, i);
            }

            var wanted = new HashSet<string>(StringComparer.Ordinal);
            AddKnownMarkers(wanted);
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                for (var e = 0; e < registration.Dependencies.Count; e++) {
                    var edge = registration.Dependencies[e];
                    if (edge.Kind != "Collection")
                        continue;
                    if (CollectionTypes.TryElement(edge.ParameterType, out var element))
                        wanted.Add(element);
                }
            }

            // A transient is not in _exports, so even a single one gets a list: otherwise
            // ResolveAll would silently return nothing.
            foreach (var pair in byType) {
                if (pair.Value.Count > 1 || HasTransientSlot(plan, pair.Value))
                    wanted.Add(pair.Key);
            }

            foreach (var type in wanted) {
                if (byType.TryGetValue(type, out var slots) == false || slots.Count == 0)
                    continue;

                var marker = new ScopePlan.Marker {
                    ElementType = type,
                    FieldName = ScopeNames.MarkerField(type, used),
                };
                marker.Slots.AddRange(slots);
                plan.Markers.Add(marker);
                if (HasTransientSlot(plan, slots))
                    continue;

                plan.Fields.Add(new ScopePlan.Field {
                    Type = type + "[]",
                    Name = marker.FieldName,
                    IsMarker = true,
                });
            }
        }

        public static bool HasTransientSlot(ScopePlan plan, List<int> slots) {
            for (var i = 0; i < slots.Count; i++) {
                if (plan.Slots[slots[i]].IsTransient)
                    return true;
            }

            return false;
        }

        private static void AddKnownMarkers(HashSet<string> wanted) {
            wanted.Add("global::Internal.IScopeBaseSetup");
            wanted.Add("global::Internal.IScopeBaseSetupAsync");
            wanted.Add("global::Internal.IScopeSetup");
            wanted.Add("global::Internal.IScopeSetupAsync");
            wanted.Add("global::Internal.IScopeSetupCompletion");
            wanted.Add("global::Internal.IScopeSetupCompletionAsync");
            wanted.Add("global::Internal.IScopeLoaded");
            wanted.Add("global::Internal.IScopeLoadedAsync");
            wanted.Add("global::Internal.IScopeDispose");
            wanted.Add("global::Internal.IScopeDisposeAsync");
            wanted.Add("global::Internal.ISceneService");
            wanted.Add("global::Internal.IEntityComponent");
        }

        private static void AddMarker(Dictionary<string, List<int>> byType, string type, int index) {
            if (string.IsNullOrEmpty(type))
                return;
            if (byType.TryGetValue(type, out var list) == false) {
                list = new List<int>();
                byType[type] = list;
            }

            list.Add(index);
        }
    }
}
