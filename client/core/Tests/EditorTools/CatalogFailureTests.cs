using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    // A generator treats what it has read as the whole truth and deletes the rest: a failure to read is never "nothing
    // there". The scans run over a real folder without read permission.
    public class CatalogFailureTests
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string LockedRoot = "Assets/CatalogFailureTests";
        // A folder of the test: TearDown deletes every locked root, so never the real art of the project.
        private const string ArtFolder = "Assets/CatalogFailureTestsLockedArt";
        private const string OpenArtFolder = "Assets/CatalogFailureTestsArt";

        private static string SettingsFile =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), FrameworkSettings.SettingsPath);

        private string _settings;
        private readonly List<string> _folders = new();
        private readonly List<string> _temps = new();

        // A crash of the editor leaves the folders imported: CreateFolder makes "... 1", a .meta revives a deleted one.
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            foreach (var folder in new[] { LockedRoot, ArtFolder, OpenArtFolder })
            {
                if (Directory.Exists(FullPath(folder) + "/locked"))
                    Chmod("755", FullPath(folder) + "/locked");

                AssetDatabase.DeleteAsset(folder);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _settings = File.ReadAllText(SettingsFile);
        }

        [TearDown]
        public void TearDown()
        {
            File.WriteAllText(SettingsFile, _settings);

            foreach (var folder in _folders)
            {
                Chmod("755", folder + "/locked");
                Directory.Delete(folder, true);
            }

            foreach (var temp in _temps)
                Directory.Delete(temp, true);

            _folders.Clear();
            _temps.Clear();
        }

        [Test]
        public void Scan_Failure_Prefabs_Throws()
        {
            LockFolder(FullPath(LockedRoot));
            AssertThrows(() => Invoke(typeof(PrefabSourceCollector), "CollectSources"));
        }

        [Test]
        public void Scan_Failure_Assets_Throws()
        {
            LockFolder(FullPath(LockedRoot));
            AssertThrows(() => Invoke(typeof(AssetSourceCollector), "CollectSources"));
        }

        [Test]
        public void Scan_Failure_Audio_Throws()
        {
            LockFolder(FullPath(LockedRoot));
            AssertThrows(() => AudioSourceCollector.EnumerateMarked().ToList());
        }

        [Test]
        public void Scan_Failure_Sprites_Throws()
        {
            UseArtFolder();
            LockFolder(FullPath(ArtFolder));
            AssertThrows(() => Invoke(typeof(SpriteSourceCollector), "CollectSources"));
        }

        [Test]
        public void Scan_Failure_Registries_Throw()
        {
            UseArtFolder();
            LockFolder(FullPath(LockedRoot));
            LockFolder(FullPath(ArtFolder));

            AssertThrows(() => Discover(SpriteGroupsRegistry.Instance));
            AssertThrows(() => Discover(PrefabGroupsRegistry.Instance));
            AssertThrows(() => Discover(AssetGroupsRegistry.Instance));
        }

        [Test]
        public void ContainerMap_FolderUnreadable_Throws()
        {
            var root = TempAssets();
            LockFolder(root);

            AssertThrows(() => ContainerGraphAssetScanner.Scan(root));
        }

        [Test]
        public void ContainerMap_FileUnreadable_Throws()
        {
            var root = TempAssets();
            LockFolder(root);
            Chmod("755", root + "/locked");

            var file = root + "/locked/Broken.prefab";
            File.WriteAllText(file, "%YAML 1.1\n");
            Chmod("000", file);

            try
            {
                AssertThrows(() => ContainerGraphAssetScanner.Scan(root));
            }
            finally
            {
                Chmod("644", file);
            }
        }

        // The override of a variant is in a block of the prefab instance: not reading it silently drops the entity.
        [Test]
        public void ContainerMap_VariantOverrideOfAutoDetected_Throws()
        {
            var root = TempAssets();
            Directory.CreateDirectory(root);
            _folders.Add(root);
            Directory.CreateDirectory(root + "/locked");

            File.WriteAllText(root + "/Variant.prefab",
                "%YAML 1.1\n--- !u!1001 &100100000\nPrefabInstance:\n  m_Modification:\n    m_Modifications:\n" +
                "    - target: {fileID: 1, guid: 00000000000000000000000000000001, type: 3}\n" +
                "      propertyPath: _autoDetected.Array.size\n      value: 0\n      objectReference: {fileID: 0}\n");

            AssertThrows(() => ContainerGraphAssetScanner.Scan(root));
        }

        [Test]
        public void Aseprite_Unreadable_Throws_BeforeAnyDeletion()
        {
            var path = FullPath("Temp/CatalogFailureTests_broken.aseprite");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });

            try
            {
                AssertThrows(() => AsepriteDocument.Read(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void ArtFolder_IsTheOneOfTheSettings()
        {
            const string sprite = OpenArtFolder + "/Hero.png";

            // No module roots: the sprites of the game's modules would be counted as well.
            Write(json =>
            {
                json["ArtFolder"] = OpenArtFolder;
                json["ModuleRoots"] = new JArray();
            });
            AssetDatabase.CreateFolder("Assets", "CatalogFailureTestsArt");

            try
            {
                var texture = new Texture2D(2, 2);
                File.WriteAllBytes(FullPath(sprite), texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(sprite, ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(sprite);
                SpriteCatalogMetadata.TryRead(importer, out var metadata);
                metadata ??= new SpriteCatalogMetadata();
                metadata.SetIncluded(true);
                metadata.SetGroup("ArtFolderTest");
                SpriteCatalogMetadata.Write(importer, metadata);

                var sources = (IEnumerable<object>)Invoke(typeof(SpriteSourceCollector), "CollectSources");
                Assert.AreEqual(1, sources.Count(), "the art of a game in another folder is taken for no art: all its sprites are deleted");

                CollectionAssert.Contains(Discover(SpriteGroupsRegistry.Instance), "ArtFolderTest");
            }
            finally
            {
                AssetDatabase.DeleteAsset(OpenArtFolder);
            }
        }

        // The inspector offers .ase, the generator has to read what is offered.
        [Test]
        public void Sprites_AseIsASource()
        {
            Assert.IsTrue((bool)Invoke(typeof(SpriteSourcePaths), "IsSourceFile", "Assets/Art/Hero.ase"));
        }

        // A game that keeps runtime assets in its modules marks sprites there: the group carries the module name.
        [Test]
        public void ModuleAssets_AreAnArtRoot()
        {
            const string moduleRoot = "Assets/CatalogFailureTestsModule";
            const string sprite = moduleRoot + "/Assets/Icons/Hero.png";

            Write(json => json["ModuleRoots"] = new JArray(moduleRoot));
            AssetDatabase.CreateFolder("Assets", "CatalogFailureTestsModule");
            AssetDatabase.CreateFolder(moduleRoot, "Assets");
            AssetDatabase.CreateFolder(moduleRoot + "/Assets", "Icons");

            try
            {
                var texture = new Texture2D(2, 2);
                File.WriteAllBytes(FullPath(sprite), texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(sprite, ImportAssetOptions.ForceSynchronousImport);

                Assert.AreEqual("CatalogFailureTestsModule.Icons", SpriteCatalogMetadata.GetDefaultGroup(sprite));
                CollectionAssert.DoesNotContain(Discover(SpriteGroupsRegistry.Instance), "CatalogFailureTestsModule.Icons",
                    "a folder of a module without marked sprites is offered as a group");

                var importer = AssetImporter.GetAtPath(sprite);
                SpriteCatalogMetadata.TryRead(importer, out var metadata);
                metadata ??= new SpriteCatalogMetadata();
                metadata.SetIncluded(true);
                SpriteCatalogMetadata.Write(importer, metadata);

                var sources = (IEnumerable<object>)Invoke(typeof(SpriteSourceCollector), "CollectSources");
                Assert.AreEqual(1, sources.Count(), "a sprite of a module is taken for no art: its group is never generated");

                CollectionAssert.Contains(Discover(SpriteGroupsRegistry.Instance), "CatalogFailureTestsModule.Icons");
            }
            finally
            {
                AssetDatabase.DeleteAsset(moduleRoot);
            }
        }

        private string TempAssets()
        {
            var temp = Path.Combine(Path.GetTempPath(), "CatalogFailureTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            _temps.Add(temp);
            return Path.Combine(temp, "Assets");
        }

        private static List<string> Discover(object registry)
        {
            var method = FindMethod(registry.GetType(), "DiscoverGroups");
            return ((IEnumerable<string>)method.Invoke(registry, null)).ToList();
        }

        private void UseArtFolder()
        {
            Write(json => json["ArtFolder"] = ArtFolder);
        }

        private static void Write(Action<JObject> change)
        {
            var json = JObject.Parse(File.ReadAllText(SettingsFile));
            change(json);
            File.WriteAllText(SettingsFile, json.ToString());
        }

        private void LockFolder(string root)
        {
            Directory.CreateDirectory(root + "/locked");
            _folders.Add(root);
            Chmod("000", root + "/locked");
        }

        private static string FullPath(string assetPath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
        }

        private static void Chmod(string mode, string path)
        {
            using var process = Process.Start(new ProcessStartInfo("chmod", $"{mode} \"{path}\"") { UseShellExecute = false });
            process.WaitForExit();
        }

        private static object Invoke(Type type, string method, params object[] arguments)
        {
            return FindMethod(type, method).Invoke(null, arguments);
        }

        private static MethodInfo FindMethod(Type type, string method)
        {
            var found = type.GetMethod(method, Flags);
            Assert.IsNotNull(found, $"{type.Name}.{method} is not found");
            return found;
        }

        // A log instead of the exception is the bug itself, and it also fails the test as an unexpected error log.
        private static void AssertThrows(Func<object> scan)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            try
            {
                var threw = false;

                try
                {
                    var result = scan();

                    if (result is System.Collections.IEnumerable enumerable)
                        enumerable.Cast<object>().ToList();
                }
                catch (Exception exception) when (exception is not AssertionException)
                {
                    threw = true;
                    Debug.Log($"[CatalogFailureTests] Thrown as expected: {exception.GetBaseException().GetType().Name}");
                }

                Assert.IsTrue(threw, "the failure to read became an empty result");
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
