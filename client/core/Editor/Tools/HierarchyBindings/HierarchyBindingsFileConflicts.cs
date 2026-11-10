using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Internal
{
    // What forbids writing the generated class into its file: the file belongs to another class, or the class already
    // has a file in another folder.
    internal static class HierarchyBindingsFileConflicts
    {
        // The namespace line HierarchyBindingsCodeGenerator writes; a class without a namespace has none.
        private static readonly Regex DeclaredNamespace = new(@"^namespace (\S+) \{", RegexOptions.Multiline);

        // Null - the class may be written into filePath.
        public static string Find(string filePath, string typeName, string namespaceName)
        {
            return FindForeignClass(filePath, typeName, namespaceName) ??
                   FindSecondFile(filePath, typeName, namespaceName);
        }

        // The file at the path is generated for another class: writing would silently replace that class.
        private static string FindForeignClass(string filePath, string typeName, string namespaceName)
        {
            if (File.Exists(CatalogPaths.ToFullPath(filePath)) == false)
                return null;

            var declared = ReadNamespace(filePath);

            if (string.Equals(declared, namespaceName, StringComparison.Ordinal))
                return null;

            return $"{filePath} already holds {FullName(typeName, declared)} of another object. " +
                   "Give these bindings another name.";
        }

        // The folder of the generated code follows the assembly of the owner script or of the asset, so an asset moved
        // to another assembly (or a file moved by hand) resolves to a new folder while the class still lies in the old
        // one: a second file would declare the class twice.
        private static string FindSecondFile(string filePath, string typeName, string namespaceName)
        {
            var fileName = Path.GetFileName(filePath);

            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript " + typeName, new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (string.Equals(path, filePath, StringComparison.Ordinal) ||
                    string.Equals(Path.GetFileName(path), fileName, StringComparison.Ordinal) == false)
                    continue;

                if (string.Equals(ReadNamespace(path), namespaceName, StringComparison.Ordinal) == false)
                    continue;

                return $"{FullName(typeName, namespaceName)} is already generated into {path}, and this object " +
                       $"resolves to {filePath}: a second file would declare the class twice. Move the asset back " +
                       "under the assembly of that file, or move the file with its .meta to the new path.";
            }

            return null;
        }

        private static string ReadNamespace(string filePath)
        {
            var match = DeclaredNamespace.Match(File.ReadAllText(CatalogPaths.ToFullPath(filePath)));
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string FullName(string typeName, string namespaceName)
        {
            return string.IsNullOrEmpty(namespaceName) ? typeName : namespaceName + "." + typeName;
        }
    }
}
