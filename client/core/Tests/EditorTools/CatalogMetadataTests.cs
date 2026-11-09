using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private const string HolderPath = TempFolder + "/UserData.prefab";
        private const string RegistryPath = "Temp/CatalogMetadataTests/Registry.json";

        // One prefab for the tests of the userData: they set it on its importer in memory, each test its own. The
        // assets of the other tests have their own names and stay until the end of the class too.
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            MakePrefab("UserData");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorTestAssets.DeleteFolder(TempFolder);
        }

        [TearDown]
        public void TearDown()
        {
            AssetImporter.GetAtPath(HolderPath).userData = string.Empty;

            var registry = CatalogPaths.ToFullPath(RegistryPath);

            if (File.Exists(registry))
                File.Delete(registry);
        }

        // A merge conflict in the registry is not an empty registry: the generator would take every preloaded group
        // for a lazy one and the next write would drop the file.
        [Test]
        public void UnreadableRegistry_Throws()
        {
            var fullPath = CatalogPaths.ToFullPath(RegistryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, "{\n<<<<<<< HEAD\n  \"groups\": [ \"A\" ]\n=======\n}\n");

            var registry = new TestRegistry(RegistryPath);

            Assert.Catch<Exception>(() => registry.GetGroups());
        }

        // The key of the old registries: silently ignored, it would turn every preloaded group into a lazy one.
        [Test]
        public void RegistryWithNonAddressable_Throws()
        {
            var fullPath = CatalogPaths.ToFullPath(RegistryPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, "{ \"groups\": [ \"A\" ], \"nonAddressable\": [ \"A\" ] }");

            var registry = new TestRegistry(RegistryPath);

            var exception = Assert.Catch<Exception>(() => registry.IsPreloaded("A"));
            Assert.That(exception.Message, Does.Contain("preloaded"));
        }

        [Test]
        public void UnreadableUserData_Throws()
        {
            var importer = AssetImporter.GetAtPath(HolderPath);
            importer.userData = "<<<<<<< HEAD";

            Assert.Catch<Exception>(() => AssetUserData.TryRead(importer, "prefabCatalog", "CatalogMetadataTests", out _));
        }

        [Test]
        public void StagingOverUnreadableUserData_Throws()
        {
            var importer = AssetImporter.GetAtPath(HolderPath);
            importer.userData = "<<<<<<< HEAD";

            Assert.Catch<Exception>(() => AssetUserData.Stage(importer, "prefabCatalog", "CatalogMetadataTests",
                new Newtonsoft.Json.Linq.JObject()));
        }

        // A prefab taken out of the catalogue (its .meta pulled with included: false) must leave the catalogue.
        [Test]
        public void ExcludedPrefabReimported_SchedulesCatalogue()
        {
            var path = MakePrefab("ExcludedPrefab");
            PrefabCatalogMetadata.Write(AssetImporter.GetAtPath(path),
                new PrefabCatalogMetadata(false, "CatalogMetadataTests"));

            Unschedule(typeof(PrefabCatalogGenerator));
            Reimport(typeof(PrefabCatalogPostprocessor), path);

            Assert.That(IsScheduled(typeof(PrefabCatalogGenerator)), Is.True);
        }

        [Test]
        public void ExcludedSoundReimported_SchedulesCatalogue()
        {
            var path = MakeWav("ExcludedSound");
            AudioCatalogMetadata.Write(AssetImporter.GetAtPath(path),
                new AudioCatalogMetadata(false, "CatalogMetadataTests"));

            Unschedule(typeof(AudioCatalogGenerator));
            Reimport(typeof(AudioCatalogPostprocessor), path);

            Assert.That(IsScheduled(typeof(AudioCatalogGenerator)), Is.True);
        }

        // The userData in the .meta of the games: what the catalogues read today must keep its values and be written
        // back byte for byte, next to the keys of other tools.
        [Test]
        public void SpriteUserData_ReadAndWrittenUnchanged()
        {
            var importer = ImporterWith(@"{
                ""otherTool"": { ""value"": 1 },
                ""spriteCatalog"": {
                    ""included"": true,
                    ""group"": ""Game.Players"",
                    ""kind"": ""MultipleAnimations"",
                    ""time"": 0.5,
                    ""color"": { ""r"": 0.25, ""g"": 0.5, ""b"": 0.75, ""a"": 1.0 },
                    ""fps"": 24.0,
                    ""animations"": [ { ""tag"": ""Idle"", ""layers"": [ ""Body"", ""Face"" ] } ],
                    ""customAnimations"": [ { ""name"": ""IdleFace"", ""source"": ""Idle"", ""layers"": [ ""Face"" ] } ],
                    ""tagSpeeds"": [ { ""tag"": ""Idle"", ""fps"": 6.0 } ]
                }
            }");
            var expected = importer.userData;

            Assert.That(SpriteCatalogMetadata.TryRead(importer, out var metadata), Is.True);
            Assert.That(metadata.Included, Is.True);
            Assert.That(metadata.Group, Is.EqualTo("Game.Players"));
            Assert.That(metadata.Kind, Is.EqualTo(SpriteCatalogKind.MultipleAnimations));
            Assert.That(metadata.Time, Is.EqualTo(0.5f));
            Assert.That(metadata.Color, Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)));
            Assert.That(metadata.FramesPerSecond, Is.EqualTo(24f));
            Assert.That(metadata.FindAnimation("Idle").Layers, Is.EqualTo(new[] { "Body", "Face" }));
            Assert.That(metadata.CustomAnimations[0].Name, Is.EqualTo("IdleFace"));
            Assert.That(metadata.CustomAnimations[0].Source, Is.EqualTo("Idle"));
            Assert.That(metadata.CustomAnimations[0].Layers, Is.EqualTo(new[] { "Face" }));
            Assert.That(metadata.GetFramesPerSecond("Idle"), Is.EqualTo(6f));
            Assert.That(metadata.GetFramesPerSecond("Run"), Is.EqualTo(24f));

            SpriteCatalogMetadata.Stage(importer, metadata);

            Assert.That(importer.userData, Is.EqualTo(expected));
        }

        [Test]
        public void AudioUserData_ReadAndWrittenUnchanged()
        {
            var importer = ImporterWith(@"{
                ""audioCatalog"": { ""included"": true, ""group"": ""UI"", ""volume"": 0.5 }
            }");
            var expected = importer.userData;

            Assert.That(AudioCatalogMetadata.TryRead(importer, out var metadata), Is.True);
            Assert.That(metadata.Included, Is.True);
            Assert.That(metadata.Group, Is.EqualTo("UI"));
            Assert.That(metadata.Volume, Is.EqualTo(0.5f));

            AudioCatalogMetadata.Write(importer, metadata);

            Assert.That(importer.userData, Is.EqualTo(expected));
        }

        [Test]
        public void PrefabUserData_ReadAndWrittenUnchanged()
        {
            var importer = ImporterWith(@"{
                ""prefabCatalog"": {
                    ""included"": true,
                    ""group"": ""UI"",
                    ""componentType"": ""Game.HeroView, Game"",
                    ""componentGuid"": ""0123456789abcdef0123456789abcdef""
                }
            }");
            var expected = importer.userData;

            Assert.That(PrefabCatalogMetadata.TryRead(importer, out var metadata), Is.True);
            Assert.That(metadata.Included, Is.True);
            Assert.That(metadata.Group, Is.EqualTo("UI"));
            Assert.That(metadata.ComponentType, Is.EqualTo("Game.HeroView, Game"));
            Assert.That(metadata.ComponentGuid, Is.EqualTo("0123456789abcdef0123456789abcdef"));

            PrefabCatalogMetadata.Write(importer, metadata);

            Assert.That(importer.userData, Is.EqualTo(expected));
        }

        [Test]
        public void AssetUserData_ReadAndWrittenUnchanged()
        {
            var importer = ImporterWith(@"{
                ""assetCatalog"": { ""included"": true, ""group"": ""Configs"" }
            }");
            var expected = importer.userData;

            Assert.That(AssetCatalogMetadata.TryRead(importer, out var metadata), Is.True);
            Assert.That(metadata.Included, Is.True);
            Assert.That(metadata.Group, Is.EqualTo("Configs"));

            AssetCatalogMetadata.Write(importer, metadata);

            Assert.That(importer.userData, Is.EqualTo(expected));
        }

        // What the Multiple Animations block of the inspector does to a file, as it reaches the .meta.
        [Test]
        public void SpriteEdits_WriteTheSameUserData()
        {
            var importer = ImporterWith("{}");
            var metadata = new SpriteCatalogMetadata();
            metadata.SetIncluded(true);
            metadata.SetGroup("Game.Players");
            metadata.SetKind(SpriteCatalogKind.MultipleAnimations);
            metadata.SetFramesPerSecond(24f);
            metadata.AddAnimation(new SpriteCatalogAnimation("Idle", new List<string> { "A", "B" }));
            metadata.FindAnimation("Idle").SetLayer("A", true);
            metadata.FindAnimation("Idle").SetLayer("B", false);
            metadata.AddCustomAnimation(new SpriteCatalogCustomAnimation("X", "Idle", new List<string> { "A" }));
            metadata.AddCustomAnimation(new SpriteCatalogCustomAnimation("Y", "Idle", new List<string> { "B" }));
            metadata.CustomAnimations[0].Rename("Z");
            metadata.CustomAnimations[0].SetSource("Run");
            metadata.CustomAnimations[0].SetLayer("C", true);
            metadata.RemoveCustomAnimationAt(1);
            metadata.SetFramesPerSecond("Idle", 6f);
            metadata.SetFramesPerSecond("Run", 24f);

            SpriteCatalogMetadata.Stage(importer, metadata);

            Assert.That(importer.userData, Is.EqualTo(JObject.Parse(@"{
                ""spriteCatalog"": {
                    ""included"": true,
                    ""group"": ""Game.Players"",
                    ""kind"": ""MultipleAnimations"",
                    ""time"": 0.8,
                    ""color"": { ""r"": 1.0, ""g"": 1.0, ""b"": 1.0, ""a"": 1.0 },
                    ""fps"": 24.0,
                    ""animations"": [ { ""tag"": ""Idle"", ""layers"": [ ""A"" ] } ],
                    ""customAnimations"": [ { ""name"": ""Z"", ""source"": ""Run"", ""layers"": [ ""A"", ""C"" ] } ],
                    ""tagSpeeds"": [ { ""tag"": ""Idle"", ""fps"": 6.0 } ]
                }
            }").ToString(Formatting.Indented)));
        }

        private static AssetImporter ImporterWith(string userData)
        {
            var importer = AssetImporter.GetAtPath(HolderPath);
            importer.userData = JObject.Parse(userData).ToString(Formatting.Indented);
            return importer;
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

        // A short PCM wav: one channel, 8 bits, 0.1 s of silence. The AudioImporter fails on a clip of a few samples.
        private static string MakeWav(string name)
        {
            EnsureFolder();

            var path = $"{TempFolder}/{name}.wav";
            var data = new byte[800];
            Array.Fill(data, (byte)128);

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
