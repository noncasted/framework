using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class PlanDisposal {
        public static void CollectDisposable(ScopePlan plan, ScopeGraph graph, TypeIndex types) {
            var disposable = types.Find("global::System.IDisposable") ??
                             types.Find("System.IDisposable");

            for (var i = graph.ConstructionOrder.Count - 1; i >= 0; i--) {
                var index = graph.ConstructionOrder[i];
                if (index < 0 || index >= plan.Slots.Count)
                    continue;

                var slot = plan.Slots[index];
                if (slot.HasField == false && string.IsNullOrEmpty(slot.FieldName))
                    continue;
                if (slot.IsTransient || slot.IsSwitch || slot.SkipEmit)
                    continue;
                if (slot.IsAlternative && slot.IsGroupPrimary == false)
                    continue;

                if (slot.IsAlternative) {
                    // The field holds the member that ran: the emitter checks it at runtime.
                    var anyDisposable = false;
                    for (var m = 0; m < slot.GroupMembers.Count; m++)
                        anyDisposable |= IsDisposable(types.Find(plan.Slots[slot.GroupMembers[m]].ImplementationType), disposable);
                    if (anyDisposable == false)
                        continue;
                }
                else {
                    var symbol = types.Find(slot.ImplementationType);
                    if (symbol == null)
                        symbol = types.Find(slot.FieldType);
                    if (IsDisposable(symbol, disposable) == false)
                        continue;
                }

                plan.DisposeOrder.Add(index);
                plan.DisposableSlots.Add(index);
            }
        }

        private static bool IsDisposable(INamedTypeSymbol? type, INamedTypeSymbol? disposable) {
            if (type == null)
                return false;
            if (disposable != null && type.Implements(disposable))
                return true;
            foreach (var implemented in type.AllInterfaces) {
                if (implemented.SpecialType == SpecialType.System_IDisposable)
                    return true;
                if (implemented.Name == "IDisposable" && implemented.ContainingNamespace?.ToDisplayString() == "System")
                    return true;
            }

            return false;
        }
    }
}
