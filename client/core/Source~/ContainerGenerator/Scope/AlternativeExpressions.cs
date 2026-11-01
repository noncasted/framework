using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    // The C# expressions of a value that depends on which branches ran: a type resolves to the last of its
    // registrations that ran, an alternative that did not run gives nothing. The plain registrations stay a bare field.
    internal static class AlternativeExpressions {
        // Slots of the registrations of a type from the last one back to the first that always runs.
        public static List<int> Chain(ScopePlan plan, string type) {
            if (plan.Providers.TryGetValue(type, out var providers) == false)
                throw new InvalidOperationException("Generator has no registration of '" + type + "'.");

            var chain = new List<int>();
            var fields = new HashSet<string>(StringComparer.Ordinal);
            for (var i = providers.Count - 1; i >= 0; i--) {
                var slot = plan.Slots[providers[i]];
                // The members of a group are one field.
                if (slot.IsTransient || slot.IsAlternative == false || fields.Add(slot.FieldName))
                    chain.Add(providers[i]);
                if (slot.IsAlternative == false)
                    break;
            }

            return chain;
        }

        // A value for a parameter of the type: throws when no registration of it ran.
        public static string Required(ScopePlan plan, string type, string literal) {
            var expression = Fold(plan, type, type, out var mayBeNull);
            return mayBeNull ? "global::Internal.GeneratedScopes.RequireAlternative(" + expression + ", " + literal + ")" : expression;
        }

        // A value for Resolve: null when no registration of the type ran, Resolve goes on to the parent.
        public static string ForResolve(ScopePlan plan, string type) {
            return Fold(plan, type, "object", out _);
        }

        // An item of a list of the type: null when the slot did not run (WithoutAbsent drops it).
        public static string Item(ScopePlan.Slot slot, string type) {
            if (slot.IsTransient == false)
                return Field(slot, type);

            return slot.IsAlternative
                ? slot.RegisteredField + " ? " + slot.CreateMethod + "() : null"
                : slot.CreateMethod + "()";
        }

        public static string Field(ScopePlan.Slot slot, string type) {
            if (slot.IsAlternative == false || slot.GroupMembers.Count <= 1 || slot.FieldType == type)
                return slot.FieldName;

            // A member of the group may not give the type: the value of another member gives nothing.
            return "(" + slot.FieldName + " as " + type + ")";
        }

        private static string Fold(ScopePlan plan, string type, string result, out bool mayBeNull) {
            var chain = Chain(plan, type);
            string? rest = null;
            mayBeNull = true;
            for (var i = chain.Count - 1; i >= 0; i--) {
                var slot = plan.Slots[chain[i]];
                if (slot.IsAlternative == false) {
                    rest = slot.IsTransient ? slot.CreateMethod + "()" : slot.FieldName;
                    mayBeNull = false;
                    continue;
                }

                if (slot.IsTransient) {
                    rest = slot.RegisteredField + " ? (" + result + ")" + slot.CreateMethod + "() : " + (rest ?? "(" + result + ")null");
                    continue;
                }

                var field = Field(slot, type);
                rest = rest == null ? field : "(" + result + ")" + field + " ?? " + rest;
            }

            return rest!;
        }
    }
}
