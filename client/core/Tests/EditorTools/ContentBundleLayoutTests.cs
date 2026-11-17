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
    // The layout of the bundles and its checks, on assets of a folder of their own: what each bundle holds, the lazy
    // targets it takes, and every error that fails the build.
    public class ContentBundleLayoutTests
    {
        private const string Folder = "Assets/ContentBundleLayoutTemp";

        [SetUp]
        public void SetUp()
        {
            EditorTestAssets.DeleteFolder(Folder);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(Folder));
        }

        [TearDown]
        public void TearDown()
        {
            EditorTestAssets.DeleteFolder(Folder);
        }

        [Test]
        public void LazyTarget_GoesIntoTheBundleThatReachesIt_WithItsCatalogEntry()
        {
            var direct = Asset<ContentTestGroup>("Direct");
            var target = Target("Target", direct);
            var references = References("References", target);

            var plan = Lay(ContentTestBundles.Create("Main", references)).Single();

            CollectionAssert.AreEquivalent(new[] { PathOf(references), PathOf(target) }, plan.Assets);
            Assert.AreEqual(new[] { $"{Folder}/ReferencesLevel.unity" }, plan.Scenes.ToArray(), "The lazy scene");
            Assert.AreEqual(new[] { Guid(references) }, plan.Roots.ToArray());
            Assert.IsTrue(plan.Implicit.ContainsKey(PathOf(direct)), "A direct reference is copied in by the engine");

            var entry = plan.Objects.Single();
            Assert.AreEqual(Guid(target), entry.Guid);
            Assert.AreEqual("Main", entry.Bundle);
            Assert.IsFalse(entry.IsSubAsset);
        }

        [Test]
        public void RootOfAnotherBundle_StaysThere_TheWalkStops()
        {
            var shared = Asset<ContentTestGroup>("Shared");
            var target = Target("Target", shared);
            var references = References("References", target);

            var plans = Lay(ContentTestBundles.Create("Main", references), ContentTestBundles.Create("Shared", shared));
            var main = plans.Single(plan => plan.Name == "Main");

            Assert.IsFalse(main.Implicit.ContainsKey(PathOf(shared)), "Held by Shared: referenced across, not copied");
            Assert.AreEqual(new[] { PathOf(shared) }, plans.Single(plan => plan.Name == "Shared").Assets.ToArray());
        }

        [Test]
        public void DirectDependencyOfTwoBundles_NoOwner_Fails()
        {
            var shared = Asset<ContentTestGroup>("Shared");
            var first = Target("First", shared);
            var second = Target("Second", shared);

            var error = Fails(ContentTestBundles.Create("A", first), ContentTestBundles.Create("B", second));

            StringAssert.Contains($"{PathOf(shared)} is needed by the content bundles A (through {PathOf(first)}) and B (through {PathOf(second)})", error);
        }

        [Test]
        public void DirectDependencyOfTwoBundles_NoOwner_IsADuplicateAsData()
        {
            var shared = Asset<ContentTestGroup>("Shared");
            var first = Target("First", shared);
            var second = Target("Second", shared);

            var layout = ContentBundleLayout.Inspect(new[] { ContentTestBundles.Create("A", first), ContentTestBundles.Create("B", second) }, new string[0]);

            var duplicate = layout.Duplicates.Single();
            Assert.AreEqual(PathOf(shared), duplicate.Path);
            Assert.AreEqual(new[] { "A", "B" }, duplicate.Bundles.ToArray());
            Assert.AreEqual(new[] { duplicate.Error }, layout.Errors.ToArray(), "Inspect throws nothing, the error is in the list");
        }

        [Test]
        public void Excluded_NoPlan_TheOthersHoldWhatTheyHoldInAFullBuild()
        {
            var target = Target("Target", null);
            var references = References("References", target);
            var other = Target("Other", null);
            var bundles = new[] { ContentTestBundles.Create("Main", references), ContentTestBundles.Create("Left", other) };

            var plans = ContentBundleLayout.Lay(bundles, new[] { "Left" }).Plans;

            Assert.AreEqual(new[] { "Main" }, plans.Select(plan => plan.Name).ToArray());
            CollectionAssert.AreEquivalent(Lay(bundles).Single(plan => plan.Name == "Main").Assets, plans.Single().Assets);
        }

        [Test]
        public void Excluded_LazyReferenceOfABuiltBundleIntoIt_Fails()
        {
            var target = Target("Target", null);
            var references = References("References", target);

            var error = Assert.Throws<BuildFailedException>(() => ContentBundleLayout.Lay(
                new[] { ContentTestBundles.Create("Main", references), ContentTestBundles.Create("Left", target) }, new[] { "Left" })).Message;

            StringAssert.Contains($"references {PathOf(target)}, held by the content bundle Left, which this build leaves out", error);
        }

        [Test]
        public void Excluded_DirectReferenceOfABuiltBundleIntoIt_Fails()
        {
            var shared = Asset<ContentTestGroup>("Shared");
            var first = Target("First", shared);

            var error = Assert.Throws<BuildFailedException>(() => ContentBundleLayout.Lay(
                new[] { ContentTestBundles.Create("Main", first), ContentTestBundles.Create("Left", shared) }, new[] { "Left" })).Message;

            StringAssert.Contains($"{PathOf(first)} of Main needs {PathOf(shared)}, held by the content bundle Left, which this build leaves out", error);
        }

        [Test]
        public void Excluded_UnknownOrEveryBundle_Fails()
        {
            var bundles = new[] { ContentTestBundles.Create("Main", Target("Target", null)) };

            StringAssert.Contains("No content bundle Other to leave out", Assert.Throws<BuildFailedException>(() => ContentBundleLayout.Lay(bundles, new[] { "Other" })).Message);
            StringAssert.Contains("leaves out every content bundle", Assert.Throws<BuildFailedException>(() => ContentBundleLayout.Lay(bundles, new[] { "Main" })).Message);
        }

        [Test]
        public void LazyTargetOfTwoBundles_NoRoot_Fails()
        {
            var target = Target("Target", null);
            var first = References("First", target);
            var second = References("Second", target);

            var error = Fails(ContentTestBundles.Create("A", first), ContentTestBundles.Create("B", second));

            StringAssert.Contains($"{PathOf(target)} is reached lazily by the content bundles A", error);
            StringAssert.Contains("make it a root of the bundle that should hold it", error);
        }

        [Test]
        public void LazyTargetOfTwoBundles_RootOfOne_Passes()
        {
            var target = Target("Target", null);
            var first = References("First", target);
            var second = References("Second", target);

            var plans = Lay(ContentTestBundles.Create("A", first, target), ContentTestBundles.Create("B", second));

            Assert.AreEqual("A", plans.SelectMany(plan => plan.Objects).Single().Bundle);
        }

        [Test]
        public void RootOfTwoBundles_Fails()
        {
            var target = Target("Target", null);

            var error = Fails(ContentTestBundles.Create("A", target), ContentTestBundles.Create("B", target));

            StringAssert.Contains($"{PathOf(target)} is a root of the content bundles A and B", error);
        }

        [Test]
        public void EmptyAndBrokenReferences_Fail_AllListed()
        {
            var empty = References("Empty", null);
            var gone = Target("Gone", null);
            var broken = References("Broken", gone);
            AssetDatabase.DeleteAsset(PathOf(gone));

            var error = Fails(ContentTestBundles.Create("Main", empty, broken));

            StringAssert.Contains("Empty.asset (ContentTestReferences Empty) _target: empty", error);
            StringAssert.Contains("Broken.asset (ContentTestReferences Broken) _target", error);
            StringAssert.Contains("is no object of the project", error);
        }

        [Test]
        public void SceneInABundle_ItsLazyReferencesFound_ItsDependencyOfAssetsToo_GoesToTheAssets()
        {
            var shared = Asset<ContentTestGroup>("Shared");
            var target = Target("Target", shared);
            Target("Holder", shared);
            var scene = Scene("Level", target, shared);

            // A new scene unloads the assets loaded before it: read again.
            target = AssetDatabase.LoadAssetAtPath<ContentTestTarget>($"{Folder}/Target.asset");
            shared = AssetDatabase.LoadAssetAtPath<ContentTestGroup>($"{Folder}/Shared.asset");
            var holder = AssetDatabase.LoadAssetAtPath<ContentTestTarget>($"{Folder}/Holder.asset");
            var plan = Lay(ContentTestBundles.Create("Main", holder, AssetDatabase.LoadAssetAtPath<SceneAsset>(scene))).Single();

            Assert.AreEqual(new[] { scene }, plan.Scenes.ToArray());
            Assert.Contains(PathOf(target), plan.Assets, "The lazy target of the scene");
            Assert.Contains(PathOf(shared), plan.Assets, "Needed by the assets and the scenes: into the assets");
            Assert.IsFalse(plan.Implicit.ContainsKey(PathOf(shared)));
            Assert.AreEqual(Guid(target), plan.Objects.Single().Guid);
            Assert.AreEqual(scene, plan.SceneEntries.Single().Path);
        }

        [Test]
        public void LazyTargetOfOneBundle_NoRoot_ItsDependencyOfTheAssetsAndTheScenesOfAnother_IsADuplicate()
        {
            var sprite = Asset<ContentTestGroup>("Sprite");
            var group = Target("Group", sprite);
            References("Main", group);
            Target("Holder", sprite);
            Target("SceneTarget", null);
            var scene = Scene("Menu", AssetDatabase.LoadAssetAtPath<ContentTestTarget>($"{Folder}/SceneTarget.asset"), sprite);

            // A new scene unloads the assets loaded before it: read again.
            var main = ContentTestBundles.Create("Main", AssetDatabase.LoadAssetAtPath<ContentTestReferences>($"{Folder}/Main.asset"));
            var menu = ContentTestBundles.Create("Menu", AssetDatabase.LoadAssetAtPath<ContentTestTarget>($"{Folder}/Holder.asset"),
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scene));
            var spritePath = $"{Folder}/Sprite.asset";

            var layout = ContentBundleLayout.Inspect(new[] { main, menu }, new string[0]);

            var duplicate = layout.Duplicates.Single();
            Assert.AreEqual(spritePath, duplicate.Path);
            Assert.AreEqual(new[] { "Main", "Menu" }, duplicate.Bundles.ToArray());
            StringAssert.Contains($"{spritePath} is needed by the content bundles Main (through {Folder}/Group.asset) and Menu (through {Folder}/Holder.asset)",
                Fails(main, menu));
        }

        [Test]
        public void NoBundle_Fails()
        {
            Assert.Throws<BuildFailedException>(() => ContentBundleLayout.Lay(new ContentBundle[0]));
        }

        [Test]
        public void TwoBundlesOfOneName_CaseAside_Fail()
        {
            var first = Target("First", null);
            var second = Target("Second", null);

            Assert.Throws<System.InvalidOperationException>(() => ContentBundleLayout.Lay(new[]
            {
                ContentTestBundles.Create("Main", first), ContentTestBundles.Create("main", second)
            }));
        }

        private static System.Collections.Generic.IReadOnlyList<ContentBundlePlan> Lay(params ContentBundle[] bundles)
        {
            return ContentBundleLayout.Lay(bundles).Plans;
        }

        private static string Fails(params ContentBundle[] bundles)
        {
            return Assert.Throws<BuildFailedException>(() => ContentBundleLayout.Lay(bundles)).Message;
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

        private static ContentTestReferences References(string name, ContentTestTarget target)
        {
            // A scene of its own: an empty one fails the build, a shared one would be a lazy target of two bundles.
            var scene = $"{Folder}/{name}Level.unity";
            File.WriteAllText(scene, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(scene, ImportAssetOptions.ForceSynchronousImport);

            var references = Asset<ContentTestReferences>(name);
            references.Init(target == null ? new ContentReference<ContentTestTarget>(string.Empty, 0) : ContentReferenceFields.Create(target),
                ContentReferenceFields.CreateScene(scene));
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

        private static string PathOf(Object asset)
        {
            return AssetDatabase.GetAssetPath(asset);
        }

        private static string Guid(Object asset)
        {
            return AssetDatabase.AssetPathToGUID(PathOf(asset));
        }
    }
}
