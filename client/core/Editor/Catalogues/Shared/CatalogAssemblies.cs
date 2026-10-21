using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // A generated class must live in the same assembly as the types it references,
    // so the target folder is chosen by that assembly's asmdef.
    public static class CatalogAssemblies
    {
        public static bool IsConsumerAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            if (assemblyName.StartsWith("Unity", StringComparison.Ordinal))
                return false;

            if (assemblyName.StartsWith("System", StringComparison.Ordinal))
                return false;

            if (assemblyName == "mscorlib" || assemblyName == "netstandard")
                return false;

            // The framework assemblies (Internal, Internal.*) take no generated code, the content assembly takes the
            // generated code that belongs to Internal.
            if (assemblyName == FrameworkSettings.ContentAssembly)
                return false;

            return assemblyName != "Internal" && assemblyName.StartsWith("Internal.", StringComparison.Ordinal) == false;
        }

        public static string GetGeneratedFolder(string logTag, string assemblyName, string internalFolder)
        {
            if (string.Equals(assemblyName, "Internal", StringComparison.Ordinal) ||
                string.Equals(assemblyName, FrameworkSettings.ContentAssembly, StringComparison.Ordinal))
                return internalFolder;

            if (TryGetAsmdefDirectory(logTag, assemblyName, out var directory) == false)
            {
                Debug.LogError($"[{logTag}] asmdef '{assemblyName}' not found");
                return internalFolder;
            }

            return GetConsumerGeneratedFolder(directory);
        }

        public static string GetConsumerGeneratedFolder(string asmdefDirectory)
        {
            if (ModuleAssetLayout.IsModuleRoot(asmdefDirectory))
                return ModuleAssetLayout.GetGeneratedFolder(asmdefDirectory);

            return $"{asmdefDirectory}/Generated";
        }

        public static void ParseTypeName(string qualifiedName, out string fullName, out string assemblyName)
        {
            var comma = qualifiedName.IndexOf(',');

            if (comma < 0)
            {
                fullName = qualifiedName.Trim();
                assemblyName = string.Empty;
                return;
            }

            fullName = qualifiedName.Substring(0, comma).Trim();
            assemblyName = qualifiedName.Substring(comma + 1).Trim();
            var assemblyComma = assemblyName.IndexOf(',');

            if (assemblyComma > 0)
                assemblyName = assemblyName.Substring(0, assemblyComma).Trim();
        }

        private static bool TryGetAsmdefDirectory(string logTag, string assemblyName, out string directory)
        {
            directory = null;

            foreach (var guid in AssetDatabase.FindAssets("t:asmdef"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                try
                {
                    var fullPath = CatalogPaths.ToFullPath(path);

                    if (File.Exists(fullPath) == false)
                        continue;

                    var file = JsonConvert.DeserializeObject<AsmdefFile>(File.ReadAllText(fullPath));

                    if (file == null || string.Equals(file.name, assemblyName, StringComparison.Ordinal) == false)
                        continue;

                    directory = Path.GetDirectoryName(path).Replace('\\', '/');
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[{logTag}] Failed to read {path}: {exception}");
                }
            }

            return false;
        }

        private sealed class AsmdefFile
        {
            public string name;
        }
    }
}