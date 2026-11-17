using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    // A broken input of a catalogue stops the run: left out of the sources, its member is dropped, and the stale cleanup
    // deletes the group asset and the class of a group it was the last of.
    public class CatalogBrokenInputTests
    {
        private const string Folder = "Assets/CatalogBrokenInputTests";

        [SetUp]
        public void SetUp()
        {
            // A crash of the editor leaves the folder, and the next one would be "CatalogBrokenInputTests 1".
            DeleteFolder();
            AssetDatabase.CreateFolder("Assets", "CatalogBrokenInputTests");
        }

        [TearDown]
        public void TearDown()
        {
            DeleteFolder();
        }

        [Test]
        public void Prefab_EmptyGroup_Throws()
        {
            var path = CreatePrefab("Hero.prefab");
            PrefabCatalogMetadata.Write(AssetImporter.GetAtPath(path), new PrefabCatalogMetadata(true, string.Empty));

            AssertThrowsWithPath(path, () => PrefabSourceCollector.CollectSources());
        }

        [Test]
        public void Prefab_WithoutImporter_Throws()
        {
            var path = Folder + "/NotImported.prefab";
            File.WriteAllText(CatalogPaths.ToFullPath(path), "%YAML 1.1\n");

            AssertThrowsWithPath(path, () => PrefabSourceCollector.CollectSources());
        }

        // Unity imports nothing in a `~` folder: such a file is no source and has no importer by design.
        [Test]
        public void Prefab_InIgnoredFolder_IsNoSource()
        {
            var path = Folder + "/Source~/Hidden.prefab";
            Directory.CreateDirectory(CatalogPaths.ToFullPath(Folder + "/Source~"));
            File.WriteAllText(CatalogPaths.ToFullPath(path), "%YAML 1.1\n");

            foreach (var source in PrefabSourceCollector.CollectSources())
                Assert.AreNotEqual(path, source.Path);
        }

        [Test]
        public void Asset_NotAnEnvAsset_Throws()
        {
            var path = Folder + "/Clip.asset";
            AssetDatabase.CreateAsset(new AnimationClip(), path);
            AssetCatalogMetadata.Write(AssetImporter.GetAtPath(path), new AssetCatalogMetadata(true, "BrokenInputTests"));

            AssertThrowsWithPath(path, () => AssetSourceCollector.CollectSources());
        }

        private static string CreatePrefab(string name)
        {
            var path = Folder + "/" + name;
            var source = new GameObject("source");
            PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);
            return path;
        }

        private static void AssertThrowsWithPath(string path, Action collect)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => collect());
            StringAssert.Contains(path, exception.Message);
        }

        // The files never imported are not known to the asset database: deleted from the disk first.
        private static void DeleteFolder()
        {
            var fullPath = CatalogPaths.ToFullPath(Folder);

            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);

            File.Delete(fullPath + ".meta");
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }
    }
}
