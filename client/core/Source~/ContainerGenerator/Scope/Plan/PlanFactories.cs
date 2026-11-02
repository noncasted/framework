using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class PlanFactories {
        public static void BuildFactories(ScopePlan plan, ScopeGraph graph, HashSet<string> used) {
            for (var i = 0; i < plan.Slots.Count; i++) {
                var slot = plan.Slots[i];
                if (slot.IsSwitch == false)
                    continue;

                var returnType = slot.Registration.FirstService();
                if (string.IsNullOrEmpty(returnType))
                    returnType = slot.ImplementationType;
                if (string.IsNullOrEmpty(returnType) && slot.Registration.Arms.Count > 0)
                    returnType = slot.Registration.Arms[0].ImplementationType;
                if (string.IsNullOrEmpty(returnType))
                    returnType = "object";

                var discriminantType = slot.Registration.InferDiscriminantType();
                plan.Factories.Add(new ScopePlan.Factory {
                    Slot = i,
                    MethodName = slot.CreateMethod,
                    ReturnType = returnType,
                    DiscriminantType = discriminantType,
                });
            }
        }
    }
}
