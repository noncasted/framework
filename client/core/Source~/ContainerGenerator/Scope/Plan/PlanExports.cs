using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class PlanExports {
        // A type resolves to its last registration that ran, as a constructor parameter binds (ParameterBinder): when
        // a transient is among them, the type is not exported and the transient branch of Resolve creates it.
        public static void BuildExports(ScopePlan plan, ScopeGraph graph) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.SkipEmit)
                    continue;
                if (slot.IsTransient == false && slot.HasField == false && string.IsNullOrEmpty(slot.FieldName))
                    continue;

                var registration = graph.Registrations[i];
                for (var s = 0; s < registration.ServiceTypes.Count; s++)
                    AddProvider(plan, registration.ServiceTypes[s], i);
                AddProvider(plan, registration.ImplementationType, i);
            }

            foreach (var pair in plan.Providers) {
                var chain = AlternativeExpressions.Chain(plan, pair.Key);
                if (plan.HasTransientSlot(chain))
                    continue;

                plan.Exports.Add(new ScopePlan.Export {
                    ServiceType = pair.Key,
                    Slot = chain[0],
                    Value = AlternativeExpressions.ForResolve(plan, pair.Key),
                });
            }
        }

        private static void AddProvider(ScopePlan plan, string type, int slot) {
            if (string.IsNullOrEmpty(type))
                return;
            if (plan.Providers.TryGetValue(type, out var slots) == false) {
                slots = new List<int>();
                plan.Providers[type] = slots;
            }

            if (slots.Contains(slot) == false)
                slots.Add(slot);
        }

        public static void BuildProvides(ScopePlan plan, ScopeGraph graph) {
            if (plan.HasProvides == false)
                return;

            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.IsHole == false || slot.SkipEmit)
                    continue;
                if (string.IsNullOrEmpty(slot.ImplementationType))
                    continue;

                var origin = slot.Registration.Origin;
                if (origin != "PrefabInstance" && origin != "PrefabAsset" && origin != "InjectExisting")
                    continue;

                var hasConstruct = false;
                for (var e = 0; e < slot.Registration.Dependencies.Count; e++) {
                    if (slot.Registration.Dependencies[e].Source == "Construct")
                        hasConstruct = true;
                }

                if (hasConstruct == false)
                    continue;

                var duplicate = false;
                for (var p = 0; p < plan.Provides.Count; p++) {
                    if (plan.Provides[p].TargetType == slot.ImplementationType) {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                    continue;

                plan.Provides.Add(new ScopePlan.Provide {
                    TargetType = slot.ImplementationType,
                    Slot = i,
                });
            }
        }
    }
}
