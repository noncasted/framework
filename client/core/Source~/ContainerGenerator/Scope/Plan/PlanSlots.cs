using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class PlanSlots {
        public static void BuildSlots(ScopePlan plan, ScopeGraph graph, TypeIndex types, HashSet<string> used) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var registration = graph.Registrations[i];
                var slot = new ScopePlan.Slot {
                    Index = i,
                    Registration = registration,
                    ImplementationType = registration.ImplementationType,
                    IsTransient = registration.Lifetime == "Transient",
                    IsHole = IsHoleOrigin(registration.Origin),
                    IsAlternative = registration.Origin == "Alternative",
                    IsSwitch = registration.Origin == "SwitchFactory",
                    // Injectable: no field, no export, no construction, only a case in Inject().
                    SkipEmit = registration.Origin == "SceneServices" ||
                               registration.Origin == "ExternalInstaller" ||
                               registration.Origin == "Injectable",
                };

                var symbol = types.Find(registration.ImplementationType);
                if (symbol != null)
                    slot.IsUnity = IsUnityObject(symbol);

                if (slot.IsUnity && slot.IsHole == false && slot.IsSwitch == false && slot.IsTransient == false)
                    slot.IsHole = true;

                if (slot.IsSwitch)
                    slot.CreateMethod = ScopeNames.CreateMethod(
                        string.IsNullOrEmpty(registration.ImplementationType)
                            ? PlanFields.FirstService(registration)
                            : registration.ImplementationType,
                        used);

                if (slot.IsTransient && slot.SkipEmit == false)
                    slot.CreateMethod = ScopeNames.CreateMethod(registration.ImplementationType, used);

                if (slot.IsTransient && slot.IsAlternative) {
                    slot.RegisteredField = ScopeNames.Unique("_is" + ScopeNames.Short(registration.ImplementationType) + "Registered", used);
                    plan.Fields.Add(new ScopePlan.Field { Type = "bool", Name = slot.RegisteredField });
                }

                plan.Slots.Add(slot);
            }
        }

        public static void GroupAlternatives(ScopePlan plan, ScopeGraph graph) {
            var groups = BranchGroups.Build(graph.Registrations);
            for (var g = 0; g < groups.Count; g++) {
                plan.AlternativeGroups.Add(g);
                var members = groups[g];
                var primary = members[members.Count - 1];
                for (var i = 0; i < members.Count; i++) {
                    var slot = plan.Slots[members[i]];
                    slot.GroupMembers.Clear();
                    slot.GroupMembers.AddRange(members);
                    slot.IsGroupPrimary = members[i] == primary;
                    slot.AlternativeGroup = g;
                }
            }
        }

        private static bool IsUnityObject(INamedTypeSymbol type) {
            var current = type;
            while (current != null) {
                if (current.Name == "Object" && current.ContainingNamespace?.ToDisplayString() == "UnityEngine")
                    return true;
                current = current.BaseType;
            }

            return false;
        }

        private static bool IsHoleOrigin(string origin) {
            return origin == "InstanceHole" ||
                   origin == "PrefabInstance" ||
                   origin == "PrefabAsset" ||
                   origin == "SceneServices" ||
                   origin == "InjectExisting";
        }
    }
}
