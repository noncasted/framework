using System.IO;
using System.Linq;
using Internal.EditorTests.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Internal.EditorTests
{
    // The shared bundles written from the duplicates of the layout, on assets of a folder of their own: an asset two
    // bundles need directly goes into the first shared bundle that takes it, a second sync changes nothing, a root no
    // longer shared leaves with its empty bundle.
    public class ContentSharedSyncTests
    {
        private const string Folder = "Assets/ContentSharedSyncTemp";
        private const string Output = Folder + "/Shared";


        [SetUp]
        public void SetUp()
        {
            EditorTestAssets.DeleteFolder(Folder);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(Folder));
            AssetDatabase.CreateFolder(Folder, Path.GetFileName(Output));
        }

        [TearDown]
        public void TearDown()
        {
            EditorTestAssets.DeleteFolder(Folder);
        }

        [Test]
        public void Duplicates_GoIntoTheFirstSharedBundleThatTakesThem_TheLayoutThenPasses()
        {
            var (others, common, gamePlay) = Project();

            Assert.IsTrue(ContentSharedSync.Sync(Output, Shared(), others));

            var sharedCommon = Written("SharedCommon");
            Assert.AreEqual(new Object[] { common }, sharedCommon.Roots.ToArray(), "Needed by Main and Run: served with Main");
            Assert.IsTrue(sharedCommon.IsPreloaded);
            Assert.IsFalse(sharedCommon.IsGzip);
            Assert.AreEqual(new Object[] { gamePlay }, Written("SharedGamePlay").Roots.ToArray(), "Needed by Run and Boss: the rest");
            Assert.IsTrue(Written("SharedGamePlay").IsGzip);
            Assert.IsNull(Written("SharedLobby"), "Nothing shared with the lobby: no bundle");

            var plans = ContentBundleLayout.Lay(others.Concat(new[] { Written("SharedCommon"), Written("SharedGamePlay") }).ToArray()).Plans;
            Assert.IsFalse(plans.Any(plan => plan.Implicit.ContainsKey(AssetDatabase.GetAssetPath(common)) || plan.Implicit.ContainsKey(AssetDatabase.GetAssetPath(gamePlay))));
        }

        [Test]
        public void SecondSync_ChangesNoFile()
        {
            var (others, _, _) = Project();
            ContentSharedSync.Sync(Output, Shared(), others);
            var before = Directory.GetFiles(Output, "*.asset").ToDictionary(path => path, File.ReadAllText);

            Assert.IsFalse(ContentSharedSync.Sync(Output, Shared(), others));

            Assert.AreEqual(before, Directory.GetFiles(Output, "*.asset").ToDictionary(path => path, File.ReadAllText));
        }

        [Test]
        public void NoLongerShared_RootLeaves_EmptyBundleDeleted()
        {
            var (others, _, gamePlay) = Project();
            ContentSharedSync.Sync(Output, Shared(), others);

            var main = AssetDatabase.LoadAssetAtPath<ContentTestTarget>($"{Folder}/Main.asset");
            main.Init(null);
            EditorUtility.SetDirty(main);
            AssetDatabase.SaveAssets();

            Assert.IsTrue(ContentSharedSync.Sync(Output, Shared(), others));

            Assert.IsNull(Written("SharedCommon"));
            Assert.AreEqual(new Object[] { gamePlay }, Written("SharedGamePlay").Roots.ToArray());
        }

        [Test]
        public void NoSharedBundleTakesADuplicate_Fails()
        {
            var (others, _, _) = Project();

            var error = Assert.Throws<BuildFailedException>(() => ContentSharedSync.Sync(Output, new[] { Shared()[0] }, others)).Message;

            StringAssert.Contains("No shared content bundle takes an asset of Boss, Run", error);
        }

        [Test]
        public void FolderOutsideTheProject_Fails()
        {
            var (others, _, _) = Project();

            Assert.Throws<System.InvalidOperationException>(() => ContentSharedSync.Sync("Temp/Shared", Shared(), others));
        }

        private static ContentSharedBundle[] Shared()
        {
            return new ContentSharedBundle[]
            {
                new("SharedCommon", true, false, "Main"),
                new("SharedLobby", false, true, "Lobby"),
                new("SharedGamePlay", false, true)
            };
        }

        // Main needs Common, Run needs Common and GamePlay, Boss needs GamePlay; none holds them.
        private static (ContentBundle[], Object, Object) Project()
        {
            var common = Asset<ContentTestGroup>("Common");
            var gamePlay = Asset<ContentTestGroup>("GamePlay");
            var main = Target("Main", common);
            var runCommon = Target("RunCommon", common);
            var runGamePlay = Target("RunGamePlay", gamePlay);
            var boss = Target("Boss", gamePlay);

            var others = new[]
            {
                ContentTestBundles.Create("Boss", boss),
                ContentTestBundles.Create("Main", main),
                ContentTestBundles.Create("Run", runCommon, runGamePlay)
            };

            return (others, common, gamePlay);
        }

        private static ContentBundle Written(string name)
        {
            return AssetDatabase.LoadAssetAtPath<ContentBundle>($"{Output}/{name}.asset");
        }

        private static T Asset<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, $"{Folder}/{name}.asset");
            return asset;
        }

        private static ContentTestTarget Target(string name, Object direct)
        {
            var target = Asset<ContentTestTarget>(name);
            target.Init(direct);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            return target;
        }
    }
}
