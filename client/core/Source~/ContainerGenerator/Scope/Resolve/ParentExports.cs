using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class ParentExports {
        public static Dictionary<string, string> CollectParentExports(
            GraphDocument document,
            GraphMethod root,
            ScopeGraph graph) {
            var exports = new Dictionary<string, string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = root.ParentId;
            while (string.IsNullOrEmpty(current) == false && visited.Add(current)) {
                GraphMethod? method = FindMethod(document, current);
                if (method == null) {
                    graph.ParentMissing = true;
                    graph.Diagnostics.Add(ResolveDiagnostics.MissingParentManifest(
                        current,
                        ParentAssemblyOf(document, root, current),
                        root.Id));
                    return exports;
                }

                var registrations = new List<GraphRegistration>();
                RegistrationFlattener.Flatten(document, method, registrations);
                for (var i = 0; i < registrations.Count; i++)
                    IndexParentServices(registrations[i], current, exports);

                current = string.IsNullOrEmpty(method.ParentId) ? method.ParentHint : method.ParentId;
            }

            return exports;
        }

        private static GraphMethod? FindMethod(GraphDocument document, string id) {
            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id == id)
                    return document.Methods[i];
            }

            return null;
        }

        private static string ParentAssemblyOf(GraphDocument document, GraphMethod root, string parentId) {
            if (string.IsNullOrEmpty(root.ParentAssembly) == false &&
                (root.ParentId == parentId || root.ParentHint == parentId))
                return root.ParentAssembly;

            var method = FindMethod(document, parentId);
            if (method != null && string.IsNullOrEmpty(method.AssemblyName) == false)
                return method.AssemblyName;
            return root.ParentAssembly;
        }

        private static void IndexParentServices(
            GraphRegistration registration,
            string parentId,
            Dictionary<string, string> exports) {
            if (registration.Origin == "Injectable")
                return;

            var added = false;
            for (var i = 0; i < registration.ServiceTypes.Count; i++) {
                if (string.IsNullOrEmpty(registration.ServiceTypes[i]))
                    continue;
                if (exports.ContainsKey(registration.ServiceTypes[i]) == false)
                    exports.Add(registration.ServiceTypes[i], parentId);
                added = true;
            }

            if (added == false && string.IsNullOrEmpty(registration.ImplementationType) == false) {
                if (exports.ContainsKey(registration.ImplementationType) == false)
                    exports.Add(registration.ImplementationType, parentId);
            }
        }
    }
}
