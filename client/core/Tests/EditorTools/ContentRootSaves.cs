using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Internal.EditorTests
{
    // The groups of the ContentRoot every time the editor writes it, while a test listens (ContentBuildTests): what a
    // content build wrote into the root before, during and after its bundles.
    public sealed class ContentRootSaves : AssetModificationProcessor
    {
        private static List<string[]> _saves;

        public static IReadOnlyList<string[]> Saves => _saves;

        public static void Listen()
        {
            _saves = new List<string[]>();
        }

        public static void Stop()
        {
            _saves = null;
        }

        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (_saves == null || paths.Contains(ContentRootGenerator.Path) == false)
                return paths;

            var root = AssetDatabase.LoadAssetAtPath<ContentRoot>(ContentRootGenerator.Path);
            _saves.Add(root.Groups.Select(group => group.Guid).ToArray());
            return paths;
        }
    }
}
