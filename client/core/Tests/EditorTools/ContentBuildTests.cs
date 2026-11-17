using System.IO;
using System.IO.Compression;
using System.Linq;
using Internal.EditorTests.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Internal.EditorTests
{
    // The content build of a player build without the player: the steps run, the bundles of the project are laid out and
    // built for the platform of the editor, their files shipped under the names of the build and the catalog goes into
    // Resources for the time of the build; the shipment and the catalog of a WebGL build (gzip, preloaded) on files of
    // their own.
    public class ContentBuildTests
    {
        private const string Folder = "Assets/ContentBuildTemp";
        private const string BundlePath = Folder + "/ContentBuildTests.asset";
        private const string Shipped = "Temp/ContentBuildTestsShipped";

        private ContentTestScenes _scenes;

        [SetUp]
        public void SetUp()
        {
            _scenes = ContentTestScenes.Clean();
            ContentBuild.Cleanup();
            EditorTestAssets.DeleteFolder(Folder);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(Folder));
            ContentTestBuildStep.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            ContentBuild.Cleanup();
            EditorTestAssets.DeleteFolder(Folder);

            foreach (var folder in new[] { Shipped, Shipped + "Source" })
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }

            _scenes.Dispose();
        }

        [Test]
        public void DirtyOpenScene_FailsWithItsNameAndWhatToDo()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(scene);

            var error = Assert.Throws<BuildFailedException>(ContentBuildScenes.ThrowIfDirty).Message;

            StringAssert.Contains("Untitled (never saved) have unsaved changes", error);
            StringAssert.Contains("EditorSceneManager.NewScene", error);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.DoesNotThrow(ContentBuildScenes.ThrowIfDirty, "A new scene is clean");
        }

        [Test]
        public void Prepare_BuildsTheBundlesOfTheProject_CatalogInResources_CleanupRemovesIt()
        {
            var target = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(target, $"{Folder}/Target.asset");
            var bundle = ContentTestBundles.Create("ContentBuildTests", target);
            AssetDatabase.CreateAsset(bundle, BundlePath);
            AssetDatabase.SaveAssets();

            var output = ContentBuild.Prepare(ContentTestBundles.EditorTarget);

            Assert.AreSame(output, ContentBuild.Current);
            Assert.AreEqual(1, ContentTestBuildStep.Calls, "The steps run once before the layout");
            StringAssert.Contains($"<type fullname=\"{typeof(ContentTestTarget).FullName}\" preserve=\"nothing\" />", File.ReadAllText(output.LinkXml), "The types of the bundles for the linker");

            var entry = output.Catalog.Bundle("ContentBuildTests");
            Assert.AreEqual(new[] { AssetDatabase.AssetPathToGUID($"{Folder}/Target.asset") }, entry.Roots);
            Assert.IsFalse(entry.IsPreloaded, "Only a WebGL build preloads");
            StringAssert.StartsWith("ContentBuildTests.", entry.Files.Single().Name);
            Assert.IsTrue(File.Exists(Path.Combine(output.Shipped, entry.Files.Single().Name)), "Shipped under the name of the build");

            var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(ContentBuildFiles.CatalogAsset);
            Assert.IsNotNull(catalog, "The catalog in Resources");
            Assert.AreEqual("ContentBuildTests", ContentCatalog.FromJson(catalog.text).Bundle("ContentBuildTests").Name);
            Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(ContentTestBundles.EditorTarget), "Prepared already");

            ContentBuild.Cleanup();

            Assert.IsNull(ContentBuild.Current);
            Assert.IsFalse(File.Exists(ContentLinkXml.Output));
            Assert.IsFalse(AssetDatabase.IsValidFolder(ContentBuildFiles.CatalogFolder));
        }

        [Test]
        public void WebGl_FetchedGzipBundleShippedAsGzipAlone_PreloadedInTheManifest_OthersAsTheyAre()
        {
            var built = new[]
            {
                Built("Fetched", isPreloaded: false, isGzip: true),
                Built("Preloaded", isPreloaded: true, isGzip: true),
                Built("Plain", isPreloaded: false, isGzip: false)
            };

            var folder = ContentShipment.Write(built, BuildTarget.WebGL, Shipped);
            var catalog = ContentCatalogWriter.Create(built, BuildTarget.WebGL);

            Assert.AreEqual(new[] { "Fetched.1.bundle.gz", "Plain.1.bundle", "Preloaded.1.bundle" }, Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(name => name).ToArray());
            Assert.AreEqual("bytes of Fetched", Gunzip(Path.Combine(folder, "Fetched.1.bundle.gz")));
            Assert.IsTrue(catalog.Bundle("Fetched").IsGzip);
            Assert.IsFalse(catalog.Bundle("Preloaded").IsGzip, "The .data file is compressed by the build");
            Assert.IsTrue(catalog.Bundle("Preloaded").IsPreloaded);

            ContentBuildFiles.WritePreloadManifest(catalog);
            Assert.AreEqual(new[] { "Content/Preloaded.1.bundle" }, File.ReadAllLines(ContentBuildFiles.PreloadManifest));
            ContentBuildFiles.Delete();
            Assert.IsFalse(File.Exists(ContentBuildFiles.PreloadManifest));

            // Another player reads them all in place.
            var standalone = ContentCatalogWriter.Create(built, ContentTestBundles.EditorTarget);
            Assert.IsFalse(standalone.Bundles.Any(bundle => bundle.IsGzip || bundle.IsPreloaded));
        }

        private ContentBuiltBundle Built(string name, bool isPreloaded, bool isGzip)
        {
            Directory.CreateDirectory(Shipped + "Source");
            var path = $"{Shipped}Source/{name}";
            File.WriteAllText(path, $"bytes of {name}");

            var plan = new ContentBundlePlan(ContentTestBundles.Create(name, isPreloaded, isGzip, new Object[0]));
            return new ContentBuiltBundle(plan, new[] { new ContentBuiltFile(path, $"{name}.1.bundle", new FileInfo(path).Length, false) }, new string[0]);
        }

        private static string Gunzip(string path)
        {
            using var gzip = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return reader.ReadToEnd();
        }
    }
}
