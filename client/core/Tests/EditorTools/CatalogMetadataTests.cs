using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    // The markup of the catalogues: the registry json, the userData of the importers and the reimport that tells
    // the generators an asset changed.
    public class CatalogMetadataTests
    {
        private const string TempFolder = "Assets/CatalogMetadataTemp";
        private const string RegistryPath = "Temp/CatalogMetadataTests/Registry.json";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.DeleteAsset(TempFolder);

            var registry = CatalogPaths.ToFullPath(RegistryPath);

            if (File.Exists(registry))
                File.Delete(registry);
        }

        // A merge conflict in the registry is not an empty registry: the generator would move every Resources
        // group back to Addressables and the next write would drop the file.
        [Test]
        public void UnreadableRegistry_Throws()
        {
            var fullPath = CatalogPaths.ToFullPath(RegistryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, "{\n<<<<<<< HEAD\n  \"groups\": [ \"A\" ]\n=======\n}\n");

            var registry = new TestRegistry(RegistryPath);

            Assert.Catch<Exception>(() => registry.GetGroups());
        }

        [Test]
        public void UnreadableUserData_Throws()
        {
            var importer = AssetImporter.GetAtPath(MakePrefab("Broken"));
            importer.userData = "<<<<<<< HEAD";

            Assert.Catch<Exception>(() => AssetUserData.TryRead(importer, "prefabCatalog", "CatalogMetadataTests", out _));
        }

        [Test]
        public void StagingOverUnreadableUserData_Throws()
        {
            var importer = AssetImporter.GetAtPath(MakePrefab("Broken"));
            importer.userData = "<<<<<<< HEAD";

            Assert.Catch<Exception>(() => AssetUserData.Stage(importer, "prefabCatalog", "CatalogMetadataTests",
                new Newtonsoft.Json.Linq.JObject()));
        }

        // A prefab taken out of the catalogue (its .meta pulled with included: false) must leave the catalogue.
        [Test]
        public void ExcludedPrefabReimported_SchedulesCatalogue()
        {
            var path = MakePrefab("Excluded");
            PrefabCatalogMetadata.Write(AssetImporter.GetAtPath(path),
                new PrefabCatalogMetadata { Included = false, Group = "CatalogMetadataTests" });

            Unschedule(typeof(PrefabCatalogGenerator));
            Reimport(typeof(PrefabCatalogPostprocessor), path);

            Assert.That(IsScheduled(typeof(PrefabCatalogGenerator)), Is.True);
        }

        [Test]
        public void ExcludedSoundReimported_SchedulesCatalogue()
        {
            var path = MakeWav("Excluded");
            AudioCatalogMetadata.Write(AssetImporter.GetAtPath(path),
                new AudioCatalogMetadata { Included = false, Group = "CatalogMetadataTests" });

            Unschedule(typeof(AudioCatalogGenerator));
            Reimport(typeof(AudioCatalogPostprocessor), path);

            Assert.That(IsScheduled(typeof(AudioCatalogGenerator)), Is.True);
        }

        private static void Reimport(Type postprocessor, string path)
        {
            var none = Array.Empty<string>();
            postprocessor.GetMethod("OnPostprocessAllAssets", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { new[] { path }, none, none, none });
        }

        // Deleting the prefabs of other tests schedules the catalogues too: start from a runner that waits for nothing.
        private static void Unschedule(Type generator)
        {
            var runner = generator.GetField("Runner", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var tick = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction), runner, "TickSchedule");

            EditorApplication.update -= tick;
            typeof(CatalogGenerationRunner)
                .GetField("_isScheduled", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(runner, false);
        }

        private static bool IsScheduled(Type generator)
        {
            var runner = generator.GetField("Runner", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            return (bool)typeof(CatalogGenerationRunner)
                .GetField("_isScheduled", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(runner);
        }

        private static string MakePrefab(string name)
        {
            EnsureFolder();

            var path = $"{TempFolder}/{name}.prefab";
            var root = new GameObject(name);

            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return path;
        }

        // The shortest PCM wav: one channel, 8 bits, two samples.
        private static string MakeWav(string name)
        {
            EnsureFolder();

            var path = $"{TempFolder}/{name}.wav";
            var data = new byte[] { 128, 128 };

            using (var writer = new BinaryWriter(File.Create(CatalogPaths.ToFullPath(path))))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + data.Length);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(8000);
                writer.Write(8000);
                writer.Write((short)1);
                writer.Write((short)8);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(data.Length);
                writer.Write(data);
            }

            AssetDatabase.ImportAsset(path);
            return path;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(TempFolder) == false)
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempFolder));
        }

        private sealed class TestRegistry : CatalogGroupsRegistry
        {
            public TestRegistry(string assetPath)
                : base("CatalogMetadataTests", assetPath)
            {
            }

            protected override IEnumerable<string> DiscoverGroups()
            {
                return Array.Empty<string>();
            }
        }
    }
}
