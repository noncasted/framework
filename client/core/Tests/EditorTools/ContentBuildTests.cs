using System;
using System.IO;
using System.Linq;
using Internal.EditorTests.Content;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Internal.EditorTests
{
    // The content build of a player build without the player: the directory of Framework.json is built into
    // Library/Content, its index goes into Resources and its preload manifest into Library for the time of the build;
    // a broken lazy reference fails it. Framework.json is put back after every test.
    public class ContentBuildTests
    {
        public const string Name = "ContentBuildTests";

        private const string TempFolder = "Assets/ContentBuildTemp";
        private const string TargetPath = TempFolder + "/Target.asset";
        private const string MissingPath = TempFolder + "/Missing.asset";
        private const string ScenePath = TempFolder + "/Level.unity";
        private const string RootPath = TempFolder + "/Root.asset";
        private const string EmptyRootPath = TempFolder + "/EmptyRoot.asset";
        private const string MissingRootPath = TempFolder + "/MissingRoot.asset";
        private const string BrokenScenePath = TempFolder + "/BrokenLevel.unity";
        private const string BrokenSceneRootPath = TempFolder + "/BrokenSceneRoot.asset";
        private const string StaleKeysRootPath = TempFolder + "/StaleKeysRoot.asset";

        private static string SettingsFile => Path.Combine(Path.GetDirectoryName(Application.dataPath), FrameworkSettings.SettingsPath);

        private string _settings;
        private WebGLCompressionFormat _compression;
        private bool _fallback;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            EditorTestAssets.DeleteFolder(TempFolder);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempFolder));

            // A real scene: its OcclusionCullingSettings hold an empty m_SceneGUID, which is no lazy reference.
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), ScenePath);
            var scene = LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath);

            var target = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(target, TargetPath);
            var missing = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(missing, MissingPath);

            CreateRoot(RootPath, LoadableObjectIdEditorUtility.CreateLoadableObjectId(target), scene);
            CreateRoot(EmptyRootPath, default, scene);
            CreateRoot(MissingRootPath, LoadableObjectIdEditorUtility.CreateLoadableObjectId(missing), scene);

            var broken = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Broken").AddComponent<ContentTestBehaviour>();
            EditorSceneManager.SaveScene(broken, BrokenScenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateRoot(BrokenSceneRootPath, LoadableObjectIdEditorUtility.CreateLoadableObjectId(target),
                LoadableSceneIdEditorUtility.CreateLoadableSceneId(BrokenScenePath));

            // A content reference and a content scene whose keys were not written by the editor.
            var stale = ScriptableObject.CreateInstance<ContentTestReferences>();
            stale.Init(new ContentReference<ContentTestTarget>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(target), "ffffffffffffffffffffffffffffffff", 1),
                new ContentScene(scene, "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"));
            AssetDatabase.CreateAsset(stale, StaleKeysRootPath);
            AssetDatabase.SaveAssets();

            // The reference of MissingRoot now leads to a deleted asset.
            AssetDatabase.DeleteAsset(MissingPath);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorTestAssets.DeleteFolder(TempFolder);

            foreach (var folder in new[] { $"{ContentBuild.Output}/{Name}", $"{ContentBrotli.Output}/{Name}" })
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _settings = File.ReadAllText(SettingsFile);
            _compression = PlayerSettings.WebGL.compressionFormat;
            _fallback = PlayerSettings.WebGL.decompressionFallback;
            Compress(WebGLCompressionFormat.Disabled, false);
            ContentTestBuildStep.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            ContentBuild.Cleanup();
            File.WriteAllText(SettingsFile, _settings);
            PlayerSettings.WebGL.compressionFormat = _compression;
            PlayerSettings.WebGL.decompressionFallback = _fallback;
        }

        [Test]
        public void Prepare_BuildsTheDirectoryAndBakesItsIndex()
        {
            WriteSettings(RootPath, true);

            var output = ContentBuild.Prepare(BuildTarget.WebGL);
            var asset = Resources.Load<TextAsset>(ContentPaths.IndexResource(Name));
            var index = ContentIndex.FromJson(asset.text);
            var root = ObjectFiles(index, AssetDatabase.LoadAssetAtPath<ContentTestRoot>(RootPath));

            Assert.That(ContentBuild.Current, Is.SameAs(output));
            Assert.That(output.Folder, Is.EqualTo($"{ContentBuild.Output}/{Name}"));
            Assert.That(File.Exists($"{output.Folder}/{ContentManifest.HashFile}"), Is.True, "The manifest hash");
            Assert.That(index.Name, Is.EqualTo(Name));
            Assert.That(index.Files, Is.EqualTo(output.Index.Files));
            Assert.That(index.RootFiles, Is.EqualTo(root), "The root files are the files of the root");
            Assert.That(index.Files.Select(file => File.Exists($"{output.Folder}/{file.Name}")), Has.All.True);
            Assert.That(ContentTestBuildStep.Calls, Is.EqualTo(1), "The step before the content build");
        }

        [Test]
        public void Prepare_ListsAPreloadedDirectoryForTheDataOfWebGl()
        {
            WriteSettings(RootPath, true);

            var output = ContentBuild.Prepare(BuildTarget.WebGL);
            var expected = output.Index.Files.Select(file => $"Content/{Name}/{file.Name}").Append($"Content/{Name}/{ContentManifest.HashFile}");

            Assert.That(File.ReadAllLines(ContentBuildFiles.PreloadManifest), Is.EquivalentTo(expected));
        }

        [Test]
        public void Prepare_ListsNothingForANotPreloadedDirectory()
        {
            WriteSettings(RootPath, false);

            ContentBuild.Prepare(BuildTarget.WebGL);

            Assert.That(File.Exists(ContentBuildFiles.PreloadManifest), Is.False);
        }

        [Test]
        public void Prepare_ShipsBrotliFilesOfAFetchedDirectoryOfWebGlWithBrotli()
        {
            WriteSettings(RootPath, false);
            Compress(WebGLCompressionFormat.Brotli, false);

            var output = ContentBuild.Prepare(BuildTarget.WebGL);
            var index = ContentIndex.FromJson(Resources.Load<TextAsset>(ContentPaths.IndexResource(Name)).text);
            var expected = index.Files.Select(file => file.Name + ".br");

            Assert.That(index.IsBrotli, Is.True, "The baked index");
            Assert.That(output.Shipped, Is.EqualTo($"{ContentBrotli.Output}/{Name}"));
            Assert.That(Directory.GetFiles(output.Shipped).Select(Path.GetFileName), Is.EquivalentTo(expected), "Only the files of the index");

            foreach (var file in index.Files)
            {
                using var brotli = BrotliStreams.Decompress(File.OpenRead($"{output.Shipped}/{file.Name}.br"));
                using var bytes = new MemoryStream();
                brotli.CopyTo(bytes);

                Assert.That(bytes.ToArray(), Is.EqualTo(File.ReadAllBytes($"{output.Folder}/{file.Name}")), file.Name);
            }
        }

        [Test]
        public void Prepare_ShipsRawFilesWithoutBrotli()
        {
            WriteSettings(RootPath, false);
            Compress(WebGLCompressionFormat.Gzip, false);

            var output = ContentBuild.Prepare(BuildTarget.WebGL);

            Assert.That(output.Index.IsBrotli, Is.False);
            Assert.That(output.Shipped, Is.EqualTo(output.Folder));
        }

        [Test]
        public void Prepare_ShipsRawFilesOfAPreloadedDirectory()
        {
            WriteSettings(RootPath, true);
            Compress(WebGLCompressionFormat.Brotli, false);

            var output = ContentBuild.Prepare(BuildTarget.WebGL);

            Assert.That(output.Index.IsBrotli, Is.False, "The .data file is compressed whole");
            Assert.That(output.Shipped, Is.EqualTo(output.Folder));
        }

        [Test]
        public void Prepare_ShipsRawFilesOfAStandalonePlayer()
        {
            WriteSettings(RootPath, false);
            Compress(WebGLCompressionFormat.Brotli, false);

            var output = ContentBuild.Prepare(BuildTarget.StandaloneLinux64);

            Assert.That(output.Index.IsBrotli, Is.False);
            Assert.That(output.Shipped, Is.EqualTo(output.Folder));
        }

        [Test]
        public void Prepare_FailsOnBrotliWithTheDecompressionFallback()
        {
            WriteSettings(RootPath, false);
            Compress(WebGLCompressionFormat.Brotli, true);

            var exception = Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(BuildTarget.WebGL));

            Assert.That(exception.Message, Does.Contain("decompression fallback"));
            Assert.That(ContentBuild.Current, Is.Null);
        }

        [Test]
        public void Cleanup_RemovesTheIndexAndThePreloadManifest()
        {
            WriteSettings(RootPath, true);
            ContentBuild.Prepare(BuildTarget.WebGL);

            ContentBuild.Cleanup();

            Assert.That(ContentBuild.Current, Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(ContentBuildFiles.IndexFolder), Is.False);
            Assert.That(Directory.Exists(ContentBuildFiles.IndexFolder), Is.False);
            Assert.That(File.Exists($"{ContentBuildFiles.IndexFolder}.meta"), Is.False);
            Assert.That(File.Exists(ContentBuildFiles.PreloadManifest), Is.False);
        }

        [Test]
        public void Prepare_ThrowsWhenAlreadyPrepared()
        {
            WriteSettings(RootPath, false);
            ContentBuild.Prepare(BuildTarget.WebGL);

            Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(BuildTarget.WebGL));
        }

        [Test]
        public void EmptyReference_FailsTheBuild()
        {
            WriteSettings(EmptyRootPath, false);

            var exception = Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(BuildTarget.WebGL));

            Assert.That(exception.Message, Does.Contain($"{EmptyRootPath} (ContentTestRoot EmptyRoot) _target.m_LoadableObjectId: empty"));
            Assert.That(ContentBuild.Current, Is.Null);
        }

        [Test]
        public void ReferenceToADeletedAsset_FailsTheBuild()
        {
            WriteSettings(MissingRootPath, false);

            var exception = Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(BuildTarget.WebGL));

            Assert.That(exception.Message, Does.Contain("(no asset) is not in the directory"));
        }

        [Test]
        public void EmptyReferenceInAScene_FailsTheBuild()
        {
            WriteSettings(BrokenSceneRootPath, false);

            var exception = Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(BuildTarget.WebGL));

            Assert.That(exception.Message, Does.Match($"{BrokenScenePath} line \\d+: empty"));
        }

        [Test]
        public void ReferenceWithStaleKeys_FailsTheBuild()
        {
            WriteSettings(StaleKeysRootPath, false);

            var exception = Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(BuildTarget.WebGL));

            Assert.That(exception.Message, Does.Contain("_target._loadable.m_LoadableObjectId: the reference keeps ffffffffffffffffffffffffffffffff:1"));
            Assert.That(exception.Message, Does.Contain("_scene._id: the scene keeps eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"));
        }

        [Test]
        public void MissingContentSettings_FailTheBuild()
        {
            var json = JObject.Parse(_settings);
            json.Remove("Content");
            File.WriteAllText(SettingsFile, json.ToString());

            Assert.Throws<InvalidOperationException>(() => ContentBuild.Prepare(BuildTarget.WebGL));
        }

        private static void Compress(WebGLCompressionFormat format, bool fallback)
        {
            PlayerSettings.WebGL.compressionFormat = format;
            PlayerSettings.WebGL.decompressionFallback = fallback;
        }

        private static void CreateRoot(string path, LoadableObjectId target, LoadableSceneId scene)
        {
            var root = ScriptableObject.CreateInstance<ContentTestRoot>();
            root.Init(target, scene);
            AssetDatabase.CreateAsset(root, path);
        }

        private void WriteSettings(string root, bool preloaded)
        {
            var json = JObject.Parse(_settings);
            json["Content"] = new JObject
            {
                ["Name"] = Name,
                ["Roots"] = new JArray(root),
                ["IsPreloaded"] = preloaded
            };
            File.WriteAllText(SettingsFile, json.ToString());
        }

        private static System.Collections.Generic.IReadOnlyList<ContentFile> ObjectFiles(ContentIndex index, UnityEngine.Object asset)
        {
            var id = LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset);
            Assert.That(LoadableObjectIdEditorUtility.TryDeconstructLoadableObjectId(id, out var guid, out var localId, out _), Is.True);
            return index.ObjectFiles(guid.ToString(), localId);
        }
    }
}
