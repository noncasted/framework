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
    // built for the active target, their files shipped under the names of the build and the catalog goes into
    // Resources for the time of the build; the shipment and the catalog of a WebGL build (gzip, preloaded) on files of
    // their own.
    public class ContentBuildTests
    {
        private const string Folder = "Assets/ContentBuildTemp";
        private const string BundlePath = Folder + "/ContentBuildTests.asset";
        private const string Shipped = "Temp/ContentBuildTestsShipped";

        // A group of a catalogue: the ContentRoot of the project references it.
        private static string GroupPath => AssetCatalogGenerator.GroupsFolder + "/ContentBuildTestsGroup.asset";

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
            ContentRootSaves.Stop();
            ContentTestBuildStep.Reset();
            ContentBuild.Cleanup();
            EditorTestAssets.DeleteFolder(Folder);

            if (AssetDatabase.DeleteAsset(GroupPath))
                ContentRootGenerator.GenerateNow();

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

            var error = Assert.Throws<BuildFailedException>(ContentUnsavedScenes.ThrowIfDirty).Message;

            StringAssert.Contains("Untitled (never saved) have unsaved changes", error);
            StringAssert.Contains("EditorSceneManager.NewScene", error);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.DoesNotThrow(ContentUnsavedScenes.ThrowIfDirty, "A new scene is clean");
        }

        [Test]
        public void Prepare_BuildsTheBundlesOfTheProject_CatalogInResources_CleanupRemovesIt()
        {
            var target = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(target, $"{Folder}/Target.asset");
            var bundle = ContentTestBundles.Create("ContentBuildTests", target);
            AssetDatabase.CreateAsset(bundle, BundlePath);
            AssetDatabase.SaveAssets();

            var output = ContentBuild.Prepare(ContentTestBundles.Target);

            Assert.AreSame(output, ContentBuild.Current);
            Assert.AreEqual(1, ContentTestBuildStep.Calls, "The steps run once before the layout");
            StringAssert.Contains($"<type fullname=\"{typeof(ContentTestTarget).FullName}\" preserve=\"nothing\" />", File.ReadAllText(output.LinkXml), "The types of the bundles for the linker");

            var entry = output.Catalog.Bundle("ContentBuildTests");
            Assert.AreEqual(new[] { AssetDatabase.AssetPathToGUID($"{Folder}/Target.asset") }, entry.Roots);
            Assert.IsFalse(entry.IsPreloaded, "Only a WebGL build preloads");
            StringAssert.StartsWith("ContentBuildTests.", entry.Files.Single().Name);
            Assert.IsTrue(File.Exists(Path.Combine(output.Shipped, entry.Files.Single().Name)), "Shipped under the name of the build");

            var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>(ContentPlayerStamp.CatalogAsset);
            Assert.IsNotNull(catalog, "The catalog in Resources");
            Assert.AreEqual("ContentBuildTests", ContentCatalog.FromJson(catalog.text).Bundle("ContentBuildTests").Name);
            Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(ContentTestBundles.Target), "Prepared already");

            ContentBuild.Cleanup();

            Assert.IsNull(ContentBuild.Current);
            Assert.IsFalse(File.Exists(ContentLinkXml.Output));
            Assert.IsFalse(AssetDatabase.IsValidFolder(ContentPlayerStamp.CatalogFolder));
        }

        [Test]
        public void Prepare_SerializeReferenceOfABundle_ItsTypeInTheLinkXmlAsSerialized()
        {
            var part = new ContentTestPart();
            part.Init(7);
            var managed = ScriptableObject.CreateInstance<ContentTestManaged>();
            managed.Init(part);
            AssetDatabase.CreateAsset(managed, $"{Folder}/Managed.asset");
            AssetDatabase.CreateAsset(ContentTestBundles.Create("ContentBuildTests", managed), BundlePath);
            AssetDatabase.SaveAssets();

            var linkXml = File.ReadAllText(ContentBuild.Prepare(ContentTestBundles.Target).LinkXml);

            var assembly = $"<assembly fullname=\"{typeof(ContentTestPart).Assembly.GetName().Name}\">";
            var type = $"<type fullname=\"{typeof(ContentTestPart).FullName}\" preserve=\"nothing\" serialized=\"true\" />";
            StringAssert.Contains(type, linkXml, "The managed reference type of the bundled data");
            Assert.AreEqual(1, linkXml.Split(new[] { assembly }, System.StringSplitOptions.None).Length - 1, "One element of its assembly");
            StringAssert.Contains($"<type fullname=\"{typeof(ContentTestManaged).FullName}\" preserve=\"nothing\" />", linkXml, "The asset type in the same assembly");
        }

        [TestCase("GamePlay:GamePlay.Enemies.ChaseEnemyBehaviour", "GamePlay", "GamePlay.Enemies.ChaseEnemyBehaviour")]
        [TestCase("Unity.RenderPipelines.Core.Runtime:UnityEngine.Rendering.AlphaUpscaler/RuntimeResources", "Unity.RenderPipelines.Core.Runtime", "UnityEngine.Rendering.AlphaUpscaler/RuntimeResources")]
        [TestCase("Game:Global", "Game", "Global")]
        public void SerializedType_Parse_AssemblyAndTheNameAsItIs(string fqn, string assembly, string fullName)
        {
            var type = ContentSerializedType.Parse(fqn);

            Assert.AreEqual(assembly, type.Assembly);
            Assert.AreEqual(fullName, type.FullName);
        }

        [TestCase("GamePlay.Enemies.ChaseEnemyBehaviour")]
        [TestCase(":GamePlay.Enemies.ChaseEnemyBehaviour")]
        [TestCase("GamePlay:")]
        public void SerializedType_Parse_NotAssemblyAndType_Throws(string fqn)
        {
            Assert.Throws<System.FormatException>(() => ContentSerializedType.Parse(fqn));
        }

        [Test]
        public void Prepare_BundleLeftOutByAStep_NotBuiltNorInTheCatalog_TheOthersAre()
        {
            var kept = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(kept, $"{Folder}/Kept.asset");
            var left = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(left, $"{Folder}/Left.asset");
            AssetDatabase.CreateAsset(ContentTestBundles.Create("ContentBuildKept", kept), $"{Folder}/ContentBuildKept.asset");
            AssetDatabase.CreateAsset(ContentTestBundles.Create("ContentBuildLeft", left), $"{Folder}/ContentBuildLeft.asset");
            AssetDatabase.SaveAssets();
            ContentTestBuildStep.Exclude("ContentBuildLeft");

            var output = ContentBuild.Prepare(ContentTestBundles.Target);

            Assert.AreEqual(new[] { AssetDatabase.AssetPathToGUID($"{Folder}/Kept.asset") }, output.Catalog.Bundle("ContentBuildKept").Roots);
            Assert.IsFalse(output.Catalog.Bundles.Any(bundle => bundle.Name == "ContentBuildLeft"), "Not in the catalog");
            Assert.IsFalse(output.Bundles.Any(bundle => bundle.Name == "ContentBuildLeft"), "Not built");
            Assert.IsFalse(Directory.GetFiles(output.Shipped).Any(file => Path.GetFileName(file).StartsWith("ContentBuildLeft.")), "Not shipped");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<ContentBundle>($"{Folder}/ContentBuildLeft.asset"), "The project keeps the bundle");
        }

        [Test]
        public void Prepare_BundleLeftOut_ContentRootWithoutItsRoots_FullAfter()
        {
            var group = ScriptableObject.CreateInstance<EnvAssetGroupAsset>();
            AssetDatabase.CreateAsset(group, GroupPath);
            AssetDatabase.CreateAsset(ContentTestBundles.Create("ContentBuildLeft", group), $"{Folder}/ContentBuildLeft.asset");
            AssetDatabase.SaveAssets();
            ContentTestBuildStep.Exclude("ContentBuildLeft");
            var guid = AssetDatabase.AssetPathToGUID(GroupPath);
            var root = AssetDatabase.AssetPathToGUID(ContentRootGenerator.Path);
            ContentRootSaves.Listen();

            var output = ContentBuild.Prepare(ContentTestBundles.Target);

            Assert.IsTrue(output.Catalog.Bundles.Any(bundle => bundle.Roots.Contains(root)),
                "A built bundle holds the ContentRoot: a lazy reference of it to the group left out would fail the build");
            Assert.IsFalse(output.Catalog.Bundles.Any(bundle => bundle.Name == "ContentBuildLeft"), "Not in the catalog");

            var saves = ContentRootSaves.Saves;
            Assert.GreaterOrEqual(saves.Count, 2, "Written without the roots of the bundle left out, then in full");
            CollectionAssert.DoesNotContain(saves[saves.Count - 2], guid, "The bundles are built with the ContentRoot without the group");
            CollectionAssert.Contains(saves[saves.Count - 1], guid, "Then the project gets the full one back");

            var after = AssetDatabase.LoadAssetAtPath<ContentRoot>(ContentRootGenerator.Path);
            Assert.IsTrue(after.Groups.Any(reference => reference.Guid == guid), "The ContentRoot references the group again");
        }

        [Test]
        public void Prepare_LeavesOutABundleTheProjectHasNot_Fails()
        {
            var target = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(target, $"{Folder}/Target.asset");
            AssetDatabase.CreateAsset(ContentTestBundles.Create("ContentBuildTests", target), BundlePath);
            AssetDatabase.SaveAssets();
            ContentTestBuildStep.Exclude("ContentBuildNoSuchBundle");

            var error = Assert.Throws<BuildFailedException>(() => ContentBuild.Prepare(ContentTestBundles.Target)).Message;

            StringAssert.Contains("No content bundle ContentBuildNoSuchBundle to leave out of the build", error);
            Assert.IsNull(ContentBuild.Current);
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

            ContentPlayerStamp.WritePreloadManifest(catalog);
            Assert.AreEqual(new[] { "Content/Preloaded.1.bundle" }, File.ReadAllLines(ContentPlayerStamp.PreloadManifest));
            ContentPlayerStamp.Delete();
            Assert.IsFalse(File.Exists(ContentPlayerStamp.PreloadManifest));

            // Another player reads them all in place.
            var standalone = ContentCatalogWriter.Create(built, BuildTarget.StandaloneLinux64);
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
