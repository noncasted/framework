using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class MarkerWriter {
        public static void WriteMarkers(CodeWriter writer, ScopePlan plan, string indent) {
            for (var i = 0; i < plan.Markers.Count; i++) {
                var marker = plan.Markers[i];
                // A list with transients is built on every ResolveAll: a field would keep the same instances.
                if (plan.HasTransientSlot(marker.Slots))
                    continue;

                writer.AppendLine(indent + marker.FieldName + " = " + MarkerArray(plan, i) + ";");
            }
        }

        public static string MarkerArray(ScopePlan plan, int markerIndex) {
            var marker = plan.Markers[markerIndex];
            var items = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var hasAlternative = false;
            for (var s = 0; s < marker.Slots.Count; s++) {
                var slot = plan.Slots[marker.Slots[s]];
                if (slot.IsTransient) {
                    if (string.IsNullOrEmpty(slot.CreateMethod) == false) {
                        items.Add(AlternativeExpressions.Item(slot, marker.ElementType));
                        hasAlternative |= slot.IsAlternative;
                    }

                    continue;
                }

                if (string.IsNullOrEmpty(slot.FieldName))
                    continue;
                if (seen.Add(slot.FieldName) == false)
                    continue;
                items.Add(AlternativeExpressions.Item(slot, marker.ElementType));
                hasAlternative |= slot.IsAlternative;
            }

            return AlternativeExpressions.WithoutAbsent("new " + marker.ElementType + "[] { " + string.Join(", ", items) + " }", hasAlternative);
        }
    }
}
