using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    // Gives AddContent and AddContentDirectory of Play Mode the content directories of the project: the roots of the
    // Content section of ProjectSettings/Framework.json or of an entry of ExtraContent, as assets of the project; while Tools/Content/Record Load Order is on, its fetches
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
            var settings = FrameworkSettings.ContentDirectoryOf(name);
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
