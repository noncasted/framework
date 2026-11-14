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
    // Library/Content and archived, its stamp goes into Resources and its preload manifest into Library for the time of the build;
    // a broken lazy reference fails it. Framework.json is put back after every test.
    public class ContentBuildTests
    {
        public const string Name = "ContentBuildTests";
        public const string ExtraName = "ContentBuildTestsExtra";

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
        private const string ExtraTargetPath = TempFolder + "/ExtraTarget.asset";
        private const string ExtraScenePath = TempFolder + "/ExtraLevel.unity";
        private const string ExtraRootPath = TempFolder + "/ExtraRoot.asset";
        private const string SharedRootPath = TempFolder + "/SharedRoot.asset";

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

            // The roots of an extra directory: its own target and scene, or the target of Root with its own scene.
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), ExtraScenePath);
            var extraScene = LoadableSceneIdEditorUtility.CreateLoadableSceneId(ExtraScenePath);
            var extraTarget = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(extraTarget, ExtraTargetPath);
            CreateRoot(ExtraRootPath, LoadableObjectIdEditorUtility.CreateLoadableObjectId(extraTarget), extraScene);
            // The new scenes above unloaded the object of Target: loaded again from its asset.
            CreateRoot(SharedRootPath, LoadableObjectIdEditorUtility.CreateLoadableObjectId(AssetDatabase.LoadAssetAtPath<ContentTestTarget>(TargetPath)), extraScene);
            AssetDatabase.SaveAssets();

            // The reference of MissingRoot now leads to a deleted asset.
            AssetDatabase.DeleteAsset(MissingPath);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorTestAssets.DeleteFolder(TempFolder);

            foreach (var name in new[] { Name, ExtraName })
            {
                foreach (var folder in new[] { $"{ContentBuild.Output}/{name}", $"{ContentArchiveWriter.Output}/{name}", $"{ContentShipment.Output}/{name}" })
                {
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, true);
                }
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
        public void Prepare_BuildsTheDirectoryItsArchiveAndBakesItsStamp()
        {
            WriteSettings(RootPath, true);

            var output = ContentBuild.Prepare(BuildTarget.WebGL).Single();
            var stamp = Stamp();
            var index = ContentIndex.FromJson(File.ReadAllText($"{output.Shipped}/{ContentPaths.IndexFile(stamp.ManifestHash)}"));
            var root = ObjectFiles(index, AssetDatabase.LoadAssetAtPath<ContentTestRoot>(RootPath));

            Assert.That(ContentBuild.Current, Is.EqualTo(new[] { output }));
            Assert.That(output.Folder, Is.EqualTo($"{ContentBuild.Output}/{Name}"));
            Assert.That(File.Exists($"{output.Folder}/{ContentManifest.HashFile}"), Is.True, "The manifest hash");
            Assert.That(stamp.ManifestHash, Is.EqualTo(output.Index.ManifestHash));
            Assert.That(index.Name, Is.EqualTo(Name));
            Assert.That(index.Files, Is.EqualTo(output.Index.Files));
            Assert.That(index.RootFiles, Is.EqualTo(root), "The root files are the files of the root");
            Assert.That(index.Files.Select(file => File.Exists($"{output.Folder}/{file.Name}")), Has.All.True);
            Assert.That(new FileInfo($"{output.Shipped}/{ContentPaths.ArchiveFile}").Length, Is.EqualTo(index.ArchiveLength));
            Assert.That(ContentTestBuildStep.Calls, Is.EqualTo(1), "The step before the content build");
        }

        [Test]
        public void Prepare_BuildsTheExtraDirectoryNextToAPreloadedOne()
        {
            WriteSettings(RootPath, true, Extra(ExtraRootPath));

            var outputs = ContentBuild.Prepare(BuildTarget.WebGL);
            var extra = outputs[1];
            var hash = extra.Index.ManifestHash;

            Assert.That(outputs.Select(output => output.Directory.Name), Is.EqualTo(new[] { Name, ExtraName }), "In the order of the settings");
            Assert.That(ContentBuild.Current, Is.EqualTo(outputs));
            Assert.That(Stamp().IsPreloaded, Is.True);
            Assert.That(Stamp(ExtraName).IsPreloaded, Is.False);
            Assert.That(Stamp(ExtraName).ManifestHash, Is.EqualTo(hash));
            Assert.That(extra.Shipped, Is.EqualTo($"{ContentShipment.Output}/{ExtraName}"));
            Assert.That(Directory.GetFiles(extra.Shipped).Select(Path.GetFileName), Is.EquivalentTo(new[] { $"{hash}.archive", $"{hash}.index.json" }),
                "Fetched by the player");
            Assert.That(File.ReadAllLines(ContentBuildFiles.PreloadManifest),
                Is.EquivalentTo(new[] { $"Content/{Name}/{ContentPaths.ArchiveFile}", $"Content/{Name}/{ContentManifest.HashFile}" }), "Only the preloaded one");
            Assert.That(extra.Index.RootFiles, Is.EqualTo(ObjectFiles(extra.Index, AssetDatabase.LoadAssetAtPath<ContentTestRoot>(ExtraRootPath))));
            Assert.That(ObjectFiles(extra.Index, AssetDatabase.LoadAssetAtPath<ContentTestTarget>(ExtraTargetPath)), Is.Not.Empty);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => ObjectFiles(outputs[0].Index, AssetDatabase.LoadAssetAtPath<ContentTestTarget>(ExtraTargetPath)),
                "The target of the extra root is not in the first directory");
            Assert.That(ContentTestBuildStep.Calls, Is.EqualTo(1), "The step sees each directory once");
        }

        [Test]
        public void Prepare_RecordsTheTypesOfEveryDirectoryForThePlayerBuild()
        {
            WriteSettings(RootPath, false, Extra(ExtraRootPath));

            var outputs = ContentBuild.Prepare(BuildTarget.WebGL);

            Assert.That(outputs.Select(output => output.ReportDirectory).Distinct().Count(), Is.EqualTo(2), "A report directory each");

            foreach (var output in outputs)
            {
                var usage = File.ReadAllText(Path.Combine(output.ReportDirectory, ContentDirectoryBuilder.TypeUsageFile));
                Assert.That(usage, Does.Contain(typeof(ContentTestTarget).FullName), output.Directory.Name);
            }
        }

        [Test]
        public void AssetInTwoDirectories_FailsTheBuild()
        {
            WriteSettings(RootPath, false, Extra(SharedRootPath));

            var exception = Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(BuildTarget.WebGL));

            Assert.That(exception.Message, Does.Contain($"{TargetPath} in {Name} and {ExtraName}"));
            Assert.That(exception.Message, Does.Not.Contain(ExtraScenePath), "The scene of the extra root is in it alone");
            Assert.That(ContentBuild.Current, Is.Null);
        }

        [Test]
        public void TwoDirectoriesOfOneName_FailTheBuild()
        {
            WriteSettings(RootPath, false, new JObject { ["Name"] = Name.ToLowerInvariant(), ["Roots"] = new JArray(ExtraRootPath), ["IsPreloaded"] = false });

            var exception = Assert.Throws<InvalidOperationException>(() => ContentBuild.Prepare(BuildTarget.WebGL));

            Assert.That(exception.Message, Does.Contain("two content directories are named"));
        }

        [Test]
        public void Prepare_RecordsTheTypesOfTheDirectoryForThePlayerBuild()
        {
            WriteSettings(RootPath, false);

            var output = ContentBuild.Prepare(BuildTarget.WebGL).Single();
            var usage = File.ReadAllText(Path.Combine(output.ReportDirectory, ContentDirectoryBuilder.TypeUsageFile));

            // The target of the lazy reference is in the directory only: no scene of a player names its script.
            Assert.That(usage, Does.Contain(typeof(ContentTestTarget).FullName));
            Assert.That(usage, Does.Contain(typeof(ContentTestRoot).FullName));
            Assert.That(usage, Does.Contain("nativeClasses"));
        }

        [Test]
        public void Prepare_ListsThePreloadedArchiveForTheDataOfWebGl()
        {
            WriteSettings(RootPath, true);

            ContentBuild.Prepare(BuildTarget.WebGL);
            var expected = new[] { $"Content/{Name}/{ContentPaths.ArchiveFile}", $"Content/{Name}/{ContentManifest.HashFile}" };

            Assert.That(File.ReadAllLines(ContentBuildFiles.PreloadManifest), Is.EquivalentTo(expected), "The archive into the .data file");
            Assert.That(Stamp().IsPreloaded, Is.True);

            // The index is a TextAsset of Resources next to the stamp, read without a request; the json has no indentation.
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ContentBuildFiles.IndexAsset(Name));
            Assert.That(asset, Is.Not.Null, ContentBuildFiles.IndexAsset(Name));
            Assert.That(asset.text, Does.Not.Contain("\n"));
            Assert.That(ContentIndex.FromJson(asset.text).ManifestHash, Is.EqualTo(Stamp().ManifestHash));
        }

        [Test]
        public void Prepare_ListsNothingForANotPreloadedDirectory()
        {
            WriteSettings(RootPath, false);

            ContentBuild.Prepare(BuildTarget.WebGL);

            Assert.That(File.Exists(ContentBuildFiles.PreloadManifest), Is.False);
            Assert.That(Stamp().IsPreloaded, Is.False);
        }

        [Test]
        public void Prepare_ShipsTheArchiveOfTheBuildAndABrotliIndexForWebGlWithBrotli()
        {
            WriteSettings(RootPath, false);
            Compress(WebGLCompressionFormat.Brotli, false);

            var output = ContentBuild.Prepare(BuildTarget.WebGL).Single();
            var hash = output.Index.ManifestHash;
            var expected = new[] { $"{hash}.archive", $"{hash}.index.json.br" };

            Assert.That(Stamp().IsIndexBrotli, Is.True, "The baked stamp");
            Assert.That(output.Shipped, Is.EqualTo($"{ContentShipment.Output}/{Name}"));
            Assert.That(Directory.GetFiles(output.Shipped).Select(Path.GetFileName), Is.EquivalentTo(expected), "The archive and the index only");
            Assert.That(File.ReadAllBytes($"{output.Shipped}/{hash}.archive"),
                Is.EqualTo(File.ReadAllBytes($"{ContentArchiveWriter.Output}/{Name}/{ContentPaths.ArchiveFile}")), "The archive as it is");

            using var brotli = BrotliStreams.Decompress(File.OpenRead($"{output.Shipped}/{hash}.index.json.br"));
            using var reader = new StreamReader(brotli);
            Assert.That(ContentIndex.FromJson(reader.ReadToEnd()).Files, Is.EqualTo(output.Index.Files));
        }

        [Test]
        public void Prepare_ShipsARawIndexWithoutBrotli()
        {
            WriteSettings(RootPath, false);
            Compress(WebGLCompressionFormat.Gzip, false);

            var output = ContentBuild.Prepare(BuildTarget.WebGL).Single();
            var hash = output.Index.ManifestHash;

            Assert.That(Stamp().IsIndexBrotli, Is.False);
            Assert.That(Directory.GetFiles(output.Shipped).Select(Path.GetFileName), Is.EquivalentTo(new[] { $"{hash}.archive", $"{hash}.index.json" }));
        }

        [Test]
        public void Prepare_ShipsTheArchiveAsMountedForAPreloadedDirectory()
        {
            WriteSettings(RootPath, true);
            Compress(WebGLCompressionFormat.Brotli, false);

            var output = ContentBuild.Prepare(BuildTarget.WebGL).Single();

            Assert.That(Stamp().IsIndexBrotli, Is.False, "The .data file is compressed whole");
            AssertReadInPlace(output);
        }

        [Test]
        public void Prepare_ShipsTheArchiveAsMountedForAStandalonePlayer()
        {
            WriteSettings(RootPath, false);
            Compress(WebGLCompressionFormat.Brotli, false);

            var output = ContentBuild.Prepare(BuildTarget.StandaloneLinux64).Single();

            Assert.That(Stamp().IsIndexBrotli, Is.False);
            Assert.That(Stamp().IsPreloaded, Is.False);
            AssertReadInPlace(output);
        }

        private static void AssertReadInPlace(ContentBuildOutput output)
        {
            var expected = new[] { ContentPaths.ArchiveFile, ContentManifest.HashFile, ContentPaths.IndexFile(output.Index.ManifestHash) };

            Assert.That(output.Shipped, Is.EqualTo($"{ContentArchiveWriter.Output}/{Name}"));
            Assert.That(Directory.GetFiles(output.Shipped).Select(Path.GetFileName), Is.EquivalentTo(expected));
        }

        private static ContentStamp Stamp(string name = Name)
        {
            return ContentStamp.FromJson(Resources.Load<TextAsset>(ContentPaths.StampResource(name)).text);
        }

        private static JObject Extra(string root)
        {
            return new JObject { ["Name"] = ExtraName, ["Roots"] = new JArray(root), ["IsPreloaded"] = false };
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
        public void Cleanup_RemovesTheStampAndThePreloadManifest()
        {
            WriteSettings(RootPath, true);
            ContentBuild.Prepare(BuildTarget.WebGL);

            ContentBuild.Cleanup();

            Assert.That(ContentBuild.Current, Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(ContentBuildFiles.StampFolder), Is.False);
            Assert.That(Directory.Exists(ContentBuildFiles.StampFolder), Is.False);
            Assert.That(File.Exists($"{ContentBuildFiles.StampFolder}.meta"), Is.False);
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

        private void WriteSettings(string root, bool preloaded, params JObject[] extra)
        {
            var json = JObject.Parse(_settings);
            json["Content"] = new JObject
            {
                ["Name"] = Name,
                ["Roots"] = new JArray(root),
                ["IsPreloaded"] = preloaded
            };
            json["ExtraContent"] = new JArray(extra.Cast<object>().ToArray());
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
