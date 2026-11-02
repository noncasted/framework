using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class PlanHoles {
        public static void AssignHoles(ScopePlan plan, ScopeGraph graph, HashSet<string> used) {
            plan.CtorParams.Add(new ScopePlan.CtorParam {
                Type = plan.LifetimeType,
                Name = plan.LifetimeParam,
                IsLifetime = true,
            });
            plan.CtorParams.Add(new ScopePlan.CtorParam {
                Type = plan.ParentType,
                Name = plan.ParentParam,
                IsParent = true,
            });

            // The parameters of switch arms are taken from the request when the chosen arm is built.
            plan.SetNeedsRequest(HasArmParameters(graph) || plan.AlternativeGroups.Count > 0 || HasTransientAlternative(plan));
            if (plan.NeedsRequest) {
                used.Add("request");
                plan.CtorParams.Add(new ScopePlan.CtorParam {
                    Type = "global::Internal.GeneratedScopeRequest",
                    Name = "request",
                    IsRequest = true,
                });
            }

            var injectionsByType = new Dictionary<string, ScopePlan.Slot>(StringComparer.Ordinal);
            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.IsHole == false || slot.SkipEmit)
                    continue;
                if (slot.HasField == false && slot.IsAlternative == false)
                    continue;

                var preferred = slot.Registration.Hole ?? "";
                if (preferred.IndexOf(';') >= 0)
                    preferred = preferred.Substring(0, preferred.IndexOf(';')).Trim();

                slot.SetCtorParam(ScopeNames.Parameter(preferred, slot.FieldType, used));
                var isInstance = RegistrationOccurrences.IsInstanceKind(slot.Registration.Kind);
                plan.CtorParams.Add(new ScopePlan.CtorParam {
                    Type = slot.FieldType,
                    Name = slot.CtorParamName,
                    Slot = slot.Index,
                    IsInstance = isInstance,
                    Occurrence = isInstance ? RegistrationOccurrences.InstanceOccurrence(graph, slot.Index) : -1,
                });

                // Several instances may call builder.Inject with one type (identical scene services):
                // the field keeps the last one, and [Inject] is called on each of them.
                if (slot.Registration.Kind != "Inject")
                    continue;

                if (injectionsByType.TryGetValue(slot.FieldType, out var owner)) {
                    slot.RepeatInjections(owner);
                    continue;
                }

                injectionsByType.Add(slot.FieldType, slot);
                var injectionsParam = ScopeNames.Unique(slot.CtorParamName.TrimStart('@') + "Injections", used);
                slot.OwnInjections(injectionsParam, ScopeNames.Unique(slot.CtorParamName.TrimStart('@') + "Index", used));
                plan.CtorParams.Add(new ScopePlan.CtorParam {
                    Type = slot.FieldType + "[]",
                    Name = slot.InjectionsParamName,
                    IsInjections = true,
                    ElementType = slot.FieldType,
                });
            }

            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                var registration = slot.Registration;
                for (var e = 0; e < registration.Dependencies.Count; e++) {
                    var edge = registration.Dependencies[e];
                    if (edge.Kind != "Hole")
                        continue;

                    // A parent export is one value for the whole scope; a WithParameter value belongs to its
                    // registration: two registrations with a parameter of one type get their own values.
                    var isParent = string.IsNullOrEmpty(edge.ParentRootId) == false;
                    var isParameter = isParent == false;
                    var existing = isParent
                        ? FindParam(plan, p => p.IsParentHole && p.Type == edge.ParameterType)
                        : FindParam(plan, p => p.IsParameter && p.OwnerSlot == i && p.Type == edge.ParameterType);
                    if (existing != null) {
                        existing.Edges.Add(edge);
                        continue;
                    }

                    // The hole of an edge has its own field: Construct of a component is called both from the
                    // constructor and from Inject / IProvides.
                    var name = ScopeNames.Parameter(edge.ParameterName, edge.ParameterType, used);
                    var field = ScopeNames.Unique("_" + name.TrimStart('@'), used);
                    plan.Fields.Add(new ScopePlan.Field {
                        Type = edge.ParameterType,
                        Name = field,
                    });
                    var param = new ScopePlan.CtorParam {
                        Type = edge.ParameterType,
                        Name = name,
                        FieldName = field,
                        IsParentHole = isParent,
                        IsParameter = isParameter,
                        IsAlternativeParameter = isParameter && slot.IsAlternative,
                        ParameterImplementation = isParameter ? registration.ImplementationType : "",
                        OwnerSlot = isParameter ? i : -1,
                        Occurrence = isParameter ? RegistrationOccurrences.ConstructedOccurrence(graph, i) : -1,
                    };
                    param.Edges.Add(edge);
                    plan.CtorParams.Add(param);
                }
            }

        }

        private static bool HasArmParameters(ScopeGraph graph) {
            for (var i = 0; i < graph.Registrations.Count; i++) {
                var dependencies = graph.Registrations[i].Dependencies;
                for (var e = 0; e < dependencies.Count; e++) {
                    if (dependencies[e].Kind == "ArmParameter")
                        return true;
                }
            }

            return false;
        }

        private static ScopePlan.CtorParam? FindParam(ScopePlan plan, Func<ScopePlan.CtorParam, bool> match) {
            for (var i = 0; i < plan.CtorParams.Count; i++) {
                if (match(plan.CtorParams[i]))
                    return plan.CtorParams[i];
            }

            return null;
        }

        private static bool HasTransientAlternative(ScopePlan plan) {
            for (var i = 0; i < plan.Slots.Count; i++) {
                if (plan.Slots[i].IsAlternative && plan.Slots[i].IsTransient)
                    return true;
            }

            return false;
        }
    }
}
