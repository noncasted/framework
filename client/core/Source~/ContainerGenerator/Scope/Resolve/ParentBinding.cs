using System;

namespace ContainerGenerator {
    internal static class ParentBinding {
        public static void BindParents(GraphDocument document) {
            if (document == null)
                return;

            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                var hint = method.ParentHint;
                if (string.IsNullOrEmpty(hint))
                    hint = method.ParentId;
                if (string.IsNullOrEmpty(hint))
                    continue;

                var id = MatchParent(document, hint);
                if (string.IsNullOrEmpty(id)) {
                    method.MarkParentUnresolved();
                    // A root from the manifest of another assembly: its parent is checked by the assembly that owns it.
                    if (string.IsNullOrEmpty(method.AssemblyName) == false &&
                        string.Equals(method.AssemblyName, document.AssemblyName, StringComparison.Ordinal) == false)
                        continue;
                    document.Diagnostics.Add(ResolveDiagnostics.MissingParentManifest(
                        hint,
                        method.ParentAssembly,
                        method.Id));
                    continue;
                }

                method.SetParentId(id);
            }
        }

        private static string MatchParent(GraphDocument document, string hint) {
            if (document == null || string.IsNullOrEmpty(hint))
                return "";

            for (var i = 0; i < document.Methods.Count; i++) {
                if (document.Methods[i].Id == hint)
                    return document.Methods[i].Id;
            }

            return "";
        }
    }
}
