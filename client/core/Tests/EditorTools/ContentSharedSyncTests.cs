using System.IO;
using System.Linq;
using Internal.EditorTests.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
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
        public void LazyTargetOfOneBundle_SharedWithTheAssetsAndTheScenesOfAnother_GoesIntoTheSharedBundleThatServesThem()
        {
            var sprite = Asset<ContentTestGroup>("Sprite");
            var group = Target("Group", sprite);
            References("Main", group);
            Target("Holder", sprite);
            Target("SceneTarget", null);
            var scene = Scene("Menu", AssetDatabase.LoadAssetAtPath<ContentTestTarget>($"{Folder}/SceneTarget.asset"), sprite);

            // A new scene unloads the assets loaded before it: read again.
            var others = new[]
            {
                ContentTestBundles.Create("Main", AssetDatabase.LoadAssetAtPath<ContentTestReferences>($"{Folder}/Main.asset")),
                ContentTestBundles.Create("Menu", AssetDatabase.LoadAssetAtPath<ContentTestTarget>($"{Folder}/Holder.asset"),
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(scene))
            };

            Assert.IsTrue(ContentSharedSync.Sync(Output, Shared(), others));

            Assert.AreEqual(new Object[] { AssetDatabase.LoadAssetAtPath<ContentTestGroup>($"{Folder}/Sprite.asset") }, Written("SharedCommon").Roots.ToArray(),
                "Needed by Main and Menu: served with Main");
            Assert.IsNull(Written("SharedGamePlay"));

            var plans = ContentBundleLayout.Lay(others.Concat(new[] { Written("SharedCommon") }).ToArray()).Plans;
            Assert.IsFalse(plans.Single(plan => plan.Name == "Menu").Assets.Contains($"{Folder}/Sprite.asset"), "Held by SharedCommon, not by Menu");
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

        private static ContentTestReferences References(string name, ContentTestTarget target)
        {
            // A scene of its own: an empty one fails the build.
            var scene = $"{Folder}/{name}Level.unity";
            File.WriteAllText(scene, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(scene, ImportAssetOptions.ForceSynchronousImport);

            var references = Asset<ContentTestReferences>(name);
            references.Init(ContentReferenceFields.Create(target), ContentReferenceFields.CreateScene(scene));
            EditorUtility.SetDirty(references);
            AssetDatabase.SaveAssets();
            return references;
        }

        // A scene with an object whose behaviour lazily references the target and directly the other asset.
        private static string Scene(string name, ContentTestTarget target, Object direct)
        {
            var path = $"{Folder}/{name}.unity";
            var (guid, localId) = ContentEditorAssets.Keys(target);
            var directPath = AssetDatabase.GetAssetPath(direct);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var holder = new GameObject("Holder").AddComponent<ContentTestBehaviour>();
            var serialized = new SerializedObject(holder);
            serialized.FindProperty("_target._guid").stringValue = guid;
            serialized.FindProperty("_target._localId").longValue = localId;
            serialized.FindProperty("_direct").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(directPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, path);

            // An untitled scene again: the layout reads the saved one as a preview, whatever is open.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return path;
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
