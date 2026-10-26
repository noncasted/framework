using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    // The generated class must live in an assembly the user class can see:
    // the user class inherits it. So we target the assembly of a script already on the object, and only
    // if there are no own scripts or they are from another assembly - the nearest asmdef above the scene or prefab.
    // A module (FrameworkSettings.ModuleRoots) takes the code into its Assets/Generated, another asmdef - into
    // Generated next to it. There is no fallback folder: an object outside every assembly is an error, generated
    // code in a random assembly would not compile or would end up where nobody looks for it.
    internal static class HierarchyBindingsPaths
    {
        // The folder for the bindings owned by a script of the content assembly (FrameworkSettings.ContentAssembly).
        public static string ContentFolder => FrameworkSettings.Content("Runtime/Tools/PrefabHierarchy/Generated");

        private const string LogTag = "HierarchyBindingsGenerator";

        private const string PluginsFolder = "Assets/Plugins/";

        public static string ResolveGeneratedFolder(GameObject root, string assetPath)
        {
            var ownerAssembly = ResolveOwnerAssembly(root);
            var asmdefFolder = FindAsmdefFolder(assetPath);

            // A script from the base assembly is not considered an owner: the generated code references
            // hierarchy types, which live in the assembly of the asset itself, and the base cannot see them.
            if (string.IsNullOrEmpty(ownerAssembly) == false &&
                (string.IsNullOrEmpty(asmdefFolder) || IsAssetAssembly(asmdefFolder, ownerAssembly)))
                return ResolveOwnerFolder(root, assetPath, ownerAssembly);

            if (string.IsNullOrEmpty(asmdefFolder) == false)
                return CatalogAssemblies.GetConsumerGeneratedFolder(asmdefFolder);

            throw new InvalidOperationException(
                $"'{root.name}' in {assetPath}: no assembly to put the bindings into. The object has no project " +
                "script and no asmdef lies above the asset. Move the asset into a module folder " +
                "(FrameworkSettings.ModuleRoots) or add a script of that module to the object.");
        }

        public static string ResolveDefaultNamespace(GameObject root)
        {
            var owner = FindOwner(root);
            return owner == null ? string.Empty : owner.GetType().Namespace ?? string.Empty;
        }

        private static string ResolveOwnerAssembly(GameObject root)
        {
            var owner = FindOwner(root);
            return owner == null ? string.Empty : owner.GetType().Assembly.GetName().Name;
        }

        // The owner is the first project script on the object. The bindings themselves are not
        // considered an owner: on regeneration they are already on the object. Plugin scripts are also
        // excluded: a plugin assembly is formally "not Unity", but our generated code must not go there.
        private static MonoBehaviour FindOwner(GameObject root)
        {
            foreach (var behaviour in root.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour is IObjectBindings)
                    continue;

                var assembly = behaviour.GetType().Assembly.GetName().Name;

                if (IsProjectAssembly(assembly) == false)
                    continue;

                if (IsProjectScript(behaviour) == false)
                    continue;

                return behaviour;
            }

            return null;
        }

        private static string ResolveOwnerFolder(GameObject root, string assetPath, string ownerAssembly)
        {
            if (string.Equals(ownerAssembly, FrameworkSettings.ContentAssembly, StringComparison.Ordinal))
                return ContentFolder;

            var asmdefPath = UnityEditor.Compilation.CompilationPipeline
                .GetAssemblyDefinitionFilePathFromAssemblyName(ownerAssembly);

            if (string.IsNullOrEmpty(asmdefPath))
                throw new InvalidOperationException(
                    $"'{root.name}' in {assetPath}: asmdef of the owner assembly '{ownerAssembly}' not found.");

            return CatalogAssemblies.GetConsumerGeneratedFolder(Path.GetDirectoryName(asmdefPath).Replace('\\', '/'));
        }

        private static bool IsProjectAssembly(string assemblyName)
        {
            if (string.Equals(assemblyName, FrameworkSettings.ContentAssembly, StringComparison.Ordinal))
                return true;

            return CatalogAssemblies.IsConsumerAssembly(assemblyName);
        }

        // A type knows nothing about where it came from, so we look at the path of the script itself.
        private static bool IsProjectScript(MonoBehaviour behaviour)
        {
            var script = MonoScript.FromMonoBehaviour(behaviour);

            if (script == null)
                return false;

            var path = AssetDatabase.GetAssetPath(script);

            if (string.IsNullOrEmpty(path))
                return false;

            if (path.StartsWith(PluginsFolder, StringComparison.Ordinal))
                return false;

            return path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static bool IsAssetAssembly(string asmdefFolder, string assemblyName)
        {
            var asmdefPath = UnityEditor.Compilation.CompilationPipeline
                .GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);

            if (string.IsNullOrEmpty(asmdefPath))
                return false;

            var folder = Path.GetDirectoryName(asmdefPath)?.Replace('\\', '/');
            return string.Equals(folder, asmdefFolder, StringComparison.Ordinal);
        }

        private static string FindAsmdefFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return string.Empty;

            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');

            while (string.IsNullOrEmpty(folder) == false && folder.StartsWith("Assets", StringComparison.Ordinal))
            {
                if (ContainsAsmdef(folder))
                    return folder;

                if (folder == "Assets")
                    return string.Empty;

                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            }

            return string.Empty;
        }

        private static bool ContainsAsmdef(string folder)
        {
            try
            {
                var fullPath = CatalogPaths.ToFullPath(folder);
                return Directory.Exists(fullPath) && Directory.GetFiles(fullPath, "*.asmdef").Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{LogTag}] Failed to scan {folder}: {exception}");
                return false;
            }
        }
    }
}