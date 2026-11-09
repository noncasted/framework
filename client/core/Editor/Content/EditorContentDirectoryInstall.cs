using System;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Internal
{
    // Gives AddContent of Play Mode the content directory of the project: the roots of the Content section of
    // ProjectSettings/Framework.json, as assets of the project.
    internal static class EditorContentDirectoryInstall
    {
        [InitializeOnLoadMethod]
        private static void Install()
        {
            EditorContentDirectories.Install(Create);
        }

        private static EditorContentDirectory Create(string name)
        {
            var settings = FrameworkSettings.ContentDirectory;

            if (settings.Name != name)
                throw new InvalidOperationException(
                    $"[Content] ContentOptions name the content directory {name}, {FrameworkSettings.SettingsPath} has {settings.Name}");

            var roots = settings.Roots.Select(Load).ToArray();
            return new EditorContentDirectory(name, roots);

            Object Load(string path)
            {
                var root = AssetDatabase.LoadAssetAtPath<Object>(path);

                if (root == null)
                    throw new InvalidOperationException($"[Content] No root asset {path} of the content directory {name}");

                return root;
            }
        }
    }
}
