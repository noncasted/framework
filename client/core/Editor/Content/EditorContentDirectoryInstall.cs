using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // Gives AddContent of Play Mode the content directory of the project: the roots of the Content section of
    // ProjectSettings/Framework.json, as assets of the project; while Tools/Content/Record Load Order is on, its fetches
    // are recorded into the load order (ContentLoadOrder).
    internal static class EditorContentDirectoryInstall
    {
        [InitializeOnLoadMethod]
        private static void Install()
        {
            EditorContentDirectories.Install(Create);
        }

        private static IContentDirectory Create(string name)
        {
            var settings = FrameworkSettings.ContentDirectory;

            if (settings.Name != name)
                throw new InvalidOperationException(
                    $"[Content] ContentOptions name the content directory {name}, {FrameworkSettings.SettingsPath} has {settings.Name}");

            var roots = settings.Roots.Select(Load).ToArray();
            var directory = new EditorContentDirectory(name, roots);

            if (ContentLoadOrder.IsRecording == false)
                return directory;

            Debug.Log($"[Content] Recording the load order of {name} into {ContentLoadOrder.PathOf(name)}");
            return new RecordingContentDirectory(directory, new ContentLoadOrderRecorder(ContentLoadOrder.PathOf(name), ContentLoadOrder.Read(name)));

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
