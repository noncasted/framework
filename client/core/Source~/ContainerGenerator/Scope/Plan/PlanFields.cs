using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class PlanFields {
        public static void AssignFields(ScopePlan plan, ScopeGraph graph, HashSet<string> used) {
            plan.Fields.Add(new ScopePlan.Field {
                Type = plan.LifetimeType,
                Name = plan.LifetimeField,
                IsLifetime = true,
            });
            plan.Fields.Add(new ScopePlan.Field {
                Type = plan.ParentType,
                Name = plan.ParentField,
            });

            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.SkipEmit || slot.IsTransient)
                    continue;
                if (slot.IsAlternative && slot.IsGroupPrimary == false)
                    continue;

                string fieldType;
                if (slot.IsAlternative && slot.IsGroupPrimary) {
                    // One member: its own type (its [Inject] and every service are reachable). Several: what they share.
                    fieldType = slot.GroupMembers.Count == 1
                        ? slot.ImplementationType
                        : CommonService(graph, slot.GroupMembers);
                    if (string.IsNullOrEmpty(fieldType))
                        fieldType = "object";
                }
                else {
                    fieldType = string.IsNullOrEmpty(slot.ImplementationType)
                        ? slot.Registration.FirstService()
                        : slot.ImplementationType;
                }

                if (string.IsNullOrEmpty(fieldType))
                    fieldType = "object";

                slot.AssignField(fieldType, ScopeNames.Field(fieldType, used));
                plan.Fields.Add(new ScopePlan.Field {
                    Type = slot.FieldType,
                    Name = slot.FieldName,
                    Slot = slot.Index,
                });

                if (slot.IsAlternative) {
                    for (var m = 0; m < slot.GroupMembers.Count; m++) {
                        var member = plan.Slots[slot.GroupMembers[m]];
                        member.ShareField(slot.FieldType, slot.FieldName);
                    }
                }
            }
        }

        private static string CommonService(ScopeGraph graph, List<int> members) {
            if (members.Count == 0)
                return "";

            var first = graph.Registrations[members[0]];
            var fallback = "";
            for (var s = 0; s < first.ServiceTypes.Count; s++) {
                var type = first.ServiceTypes[s];
                if (string.IsNullOrEmpty(type))
                    continue;

                var shared = true;
                for (var m = 1; m < members.Count; m++) {
                    if (graph.Registrations[members[m]].ServiceTypes.Contains(type) == false) {
                        shared = false;
                        break;
                    }
                }

                if (shared == false)
                    continue;

                fallback = type;
                var shortName = ScopeNames.Short(type);
                if (shortName.Length > 1 && shortName[0] == 'I' && char.IsUpper(shortName[1]))
                    return type;
            }

            return fallback;
        }
    }
}
