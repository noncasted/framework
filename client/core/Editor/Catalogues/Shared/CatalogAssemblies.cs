using System;
using System.IO;
using UnityEditor.Compilation;

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

        // A generated class can live only in an assembly with an asmdef: the content folder cannot see the types of
        // another assembly (CS0246), and the predefined Assembly-CSharp is seen by no asmdef at all.
        public static string GetGeneratedFolder(string logTag, string assemblyName, string internalFolder)
        {
            if (string.Equals(assemblyName, "Internal", StringComparison.Ordinal) ||
                string.Equals(assemblyName, FrameworkSettings.ContentAssembly, StringComparison.Ordinal))
                return internalFolder;

            return GetConsumerGeneratedFolder(GetAsmdefDirectory(logTag, assemblyName));
        }

        // The namespace of the classes generated into a project assembly is the assembly name, so it must be one.
        public static string ToNamespace(string logTag, string assemblyName)
        {
            if (CodeIdentifiers.IsNamespace(assemblyName) == false)
                throw new InvalidOperationException(
                    $"[{logTag}] Assembly '{assemblyName}' is not a C# namespace, so generated classes cannot be " +
                    "put into it. Move the scripts into an asmdef with a name made of identifiers.");

            return assemblyName;
        }

        public static string GetAsmdefDirectory(string logTag, string assemblyName)
        {
            var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);

            if (string.IsNullOrEmpty(asmdefPath))
                throw new InvalidOperationException(
                    $"[{logTag}] Assembly '{assemblyName}' has no asmdef, so generated classes cannot be put into " +
                    "it. Scripts without an asmdef compile into Assembly-CSharp: move them into an asmdef.");

            return Path.GetDirectoryName(asmdefPath).Replace('\\', '/');
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
    }
}