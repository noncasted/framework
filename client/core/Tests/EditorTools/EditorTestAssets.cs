using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Internal.EditorTests
{
    // Every call that changes assets (CreateFolder, DeleteAsset, CreateAsset, SaveAsPrefabAsset) ends with a refresh of
    // the database of its own, 60-80 ms: 280 of them were half of the EditMode run. So a temporary folder of a test
    // class lives until the end of the class and its tests delete only what they created, with one refresh.
    public static class EditorTestAssets
    {
        public static void DeleteFiles(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder) == false)
                return;

            var paths = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".meta") == false)
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            if (paths.Length == 0)
                return;

            var failed = new List<string>();
            AssetDatabase.DeleteAssets(paths, failed);
            Assert.That(failed, Is.Empty, "Not deleted");
        }

        public static void DeleteFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder);
        }
    }
}
