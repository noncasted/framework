using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class ManifestReader {
        public static void Merge(GraphDocument document, Compilation compilation) {
            if (document == null || compilation == null)
                return;

            var existing = new Dictionary<string, GraphMethod>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++)
                existing[document.Methods[i].Id] = document.Methods[i];

            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols) {
                foreach (var attribute in assembly.GetAttributes()) {
                    if (IsInstallerAttribute(attribute) == false)
                        continue;

                    var method = Read(attribute, compilation, assembly.Name);
                    if (method == null || string.IsNullOrEmpty(method.Id))
                        continue;
                    if (existing.TryGetValue(method.Id, out var local)) {
                        // A local entry without a body (a call into the referenced assembly) takes the manifest one:
                        // its registrations and its calls (an installer may only call other installers).
                        if (local.Registrations.Count == 0 && local.Calls.Count == 0 &&
                            (method.Registrations.Count > 0 || method.Calls.Count > 0)) {
                            local.Registrations.AddRange(method.Registrations);
                            local.Calls.AddRange(method.Calls);
                            local.CallOrdinals.AddRange(method.CallOrdinals);
                            local.CallTypeArguments.AddRange(method.CallTypeArguments);
                            local.CallAsServices.AddRange(method.CallAsServices);
                            local.CallHoles.AddRange(method.CallHoles);
                            local.CallParameters.AddRange(method.CallParameters);
                            if (local.ReturnedOrdinal < 0)
                                local.ReturnedOrdinal = method.ReturnedOrdinal;
                            if (string.IsNullOrEmpty(local.File))
                                local.File = method.File;
                            if (local.Line == 0)
                                local.Line = method.Line;
                        }

                        if (string.IsNullOrEmpty(local.ParentHint) && string.IsNullOrEmpty(method.ParentId) == false)
                            local.ParentHint = method.ParentId;
                        if (string.IsNullOrEmpty(local.ParentId) && string.IsNullOrEmpty(method.ParentId) == false)
                            local.ParentId = method.ParentId;
                        continue;
                    }

                    existing[method.Id] = method;
                    document.Methods.Add(method);
                }
            }
        }

        public static void ReportUnresolved(GraphDocument document) {
            if (document == null)
                return;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++)
                ids.Add(document.Methods[i].Id);

            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];

                // The calls of a method of a referenced assembly were checked when that assembly was compiled; this
                // one may not reference everything they reach.
                if (IsForeign(document, method) == true)
                    continue;

                for (var c = 0; c < method.Calls.Count; c++) {
                    var call = method.Calls[c];
                    if (ids.Contains(call))
                        continue;
                    if (ManifestHarvest.IsHarvest(call))
                        continue;

                    document.Diagnostics.Add(new DiagnosticInfo(
                        GraphDescriptors.UnresolvedInstaller,
                        null,
                        call,
                        method.Id,
                        method.File ?? "",
                        method.Line.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        private static bool IsForeign(GraphDocument document, GraphMethod method) {
            return string.IsNullOrEmpty(method.AssemblyName) == false &&
                   string.IsNullOrEmpty(document.AssemblyName) == false &&
                   string.Equals(method.AssemblyName, document.AssemblyName, StringComparison.Ordinal) == false;
        }

        private static bool IsInstallerAttribute(AttributeData attribute) {
            var type = attribute.AttributeClass;
            if (type == null)
                return false;
            if (type.Name != "ContainerInstallerAttribute")
                return false;
            var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
            return ns == "Internal";
        }

        private static GraphMethod? Read(AttributeData attribute, Compilation compilation, string assemblyName) {
            // The arguments did not bind: a neighbouring project of the IDE that does not compile yet reports its own errors.
            if (attribute.ConstructorArguments.Length < 8)
                return null;

            var methodId = AsString(attribute.ConstructorArguments[0]);
            var isRoot = AsBool(attribute.ConstructorArguments[1]);
            var file = AsString(attribute.ConstructorArguments[2]);
            var line = AsInt(attribute.ConstructorArguments[3]);
            var implementations = AsTypes(attribute.ConstructorArguments[4], compilation);
            var services = AsTypes(attribute.ConstructorArguments[5], compilation);
            var calls = AsStrings(attribute.ConstructorArguments[6]);
            var blob = AsString(attribute.ConstructorArguments[7]);

            var method = new GraphMethod {
                Id = methodId,
                IsRoot = isRoot,
                File = file,
                Line = line,
                AssemblyName = assemblyName ?? "",
            };
            method.Calls.AddRange(calls);
            ManifestBlobParser.ParseBlob(method, blob, implementations, services, assemblyName ?? "");
            return method;
        }

        private static List<string> AsTypes(TypedConstant constant, Compilation compilation) {
            var list = new List<string>();
            if (constant.Kind != TypedConstantKind.Array)
                return list;

            foreach (var value in constant.Values) {
                if (value.Value is ITypeSymbol type)
                    list.Add(TypeNames.ForCode(type));
                else
                    list.Add("");
            }

            return list;
        }

        private static List<string> AsStrings(TypedConstant constant) {
            var list = new List<string>();
            if (constant.Kind != TypedConstantKind.Array)
                return list;
            foreach (var value in constant.Values)
                list.Add(value.Value as string ?? "");
            return list;
        }

        private static string AsString(TypedConstant constant) {
            return constant.Value as string ?? "";
        }

        private static bool AsBool(TypedConstant constant) {
            return constant.Value is bool value && value;
        }

        private static int AsInt(TypedConstant constant) {
            if (constant.Value is int value)
                return value;
            return 0;
        }
    }
}
