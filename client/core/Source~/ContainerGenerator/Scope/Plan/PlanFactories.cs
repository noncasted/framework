using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class PlanFactories {
        public static void BuildFactories(ScopePlan plan, ScopeGraph graph, HashSet<string> used) {
            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.IsSwitch == false)
                    continue;

                var returnType = PlanFields.FirstService(slot.Registration);
                if (string.IsNullOrEmpty(returnType))
                    returnType = slot.ImplementationType;
                if (string.IsNullOrEmpty(returnType) && slot.Registration.Arms.Count > 0)
                    returnType = slot.Registration.Arms[0].ImplementationType;
                if (string.IsNullOrEmpty(returnType))
                    returnType = "object";

                var discriminantType = InferDiscriminantType(slot.Registration);
                plan.Factories.Add(new ScopePlan.Factory {
                    Slot = i,
                    MethodName = slot.CreateMethod,
                    ReturnType = returnType,
                    DiscriminantType = discriminantType,
                });
            }
        }

        public static string InferDiscriminantType(GraphRegistration registration) {
            if (registration.Arms.Count == 0)
                return "object";

            var pattern = registration.Arms[0].Discriminant ?? "";
            var dot = pattern.LastIndexOf('.');
            if (dot <= 0)
                return "object";

            var type = pattern.Substring(0, dot).Trim();
            if (type.StartsWith("global::", StringComparison.Ordinal) == false && type.IndexOf('.') >= 0)
                type = "global::" + type;
            if (string.IsNullOrEmpty(type))
                return "object";
            return type;
        }
    }
}
