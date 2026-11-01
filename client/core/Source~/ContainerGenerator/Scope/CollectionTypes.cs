using System;

namespace ContainerGenerator {
    // The parameter types the scope fills with a list: one definition for the resolver, the plan and the emitter.
    internal static class CollectionTypes {
        private static readonly string[] Prefixes = {
            "global::System.Collections.Generic.IReadOnlyList<",
            "global::System.Collections.Generic.IEnumerable<",
            "global::System.Collections.Generic.IReadOnlyCollection<",
        };

        public static bool TryElement(string typeFullName, out string elementType) {
            elementType = "";
            if (string.IsNullOrEmpty(typeFullName))
                return false;
            if (typeFullName.EndsWith("[]", StringComparison.Ordinal)) {
                elementType = typeFullName.Substring(0, typeFullName.Length - 2);
                return true;
            }

            for (var i = 0; i < Prefixes.Length; i++) {
                var prefix = Prefixes[i];
                if (typeFullName.StartsWith(prefix, StringComparison.Ordinal) == false)
                    continue;
                if (typeFullName.Length <= prefix.Length || typeFullName[typeFullName.Length - 1] != '>')
                    return false;

                elementType = typeFullName.Substring(prefix.Length, typeFullName.Length - prefix.Length - 1);
                return true;
            }

            return false;
        }
    }
}
