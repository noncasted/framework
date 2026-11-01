using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class ServiceIndex {
        public static void IndexServices(
            GraphRegistration registration,
            int index,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType) {
            // Injectable is not a service: nobody resolves it, the scope only injects a ready instance.
            if (registration.Origin == "Injectable")
                return;

            var added = false;
            for (var i = 0; i < registration.ServiceTypes.Count; i++) {
                AddService(registration.ServiceTypes[i], index, lastByType, allByType);
                added = true;
            }

            if (added == false && string.IsNullOrEmpty(registration.ImplementationType) == false)
                AddService(registration.ImplementationType, index, lastByType, allByType);
        }

        private static void AddService(
            string type,
            int index,
            Dictionary<string, int> lastByType,
            Dictionary<string, List<int>> allByType) {
            if (string.IsNullOrEmpty(type))
                return;

            lastByType[type] = index;
            if (allByType.TryGetValue(type, out var list) == false) {
                list = new List<int>();
                allByType[type] = list;
            }

            list.Add(index);
        }

        public static HashSet<string> ImplicitTypes(Compilation compilation, TypeIndex index) {
            var set = new HashSet<string>(StringComparer.Ordinal);
            AddImplicit(set, index, compilation.GetTypeByMetadataName("Internal.IContainer"));
            AddImplicit(set, index, compilation.GetTypeByMetadataName("Internal.IReadOnlyLifetime"));
            AddImplicit(set, index, compilation.GetTypeByMetadataName("Internal.ILifetime"));
            if (set.Count == 0) {
                set.Add("global::Internal.IContainer");
                set.Add("global::Internal.IReadOnlyLifetime");
                set.Add("global::Internal.ILifetime");
            }

            return set;
        }

        private static void AddImplicit(HashSet<string> set, TypeIndex index, INamedTypeSymbol? type) {
            var formatted = index.Format(type);
            if (string.IsNullOrEmpty(formatted) == false)
                set.Add(formatted);
        }
    }
}
