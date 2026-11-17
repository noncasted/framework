using System.IO;
using Internal.EditorTests.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.EditorTests
{
    // Builds the bundles of the Play Mode tests of the bundles, the loader and the catalogue groups before Play Mode
    // (ContentTestPaths): Tests (a root with a content reference to a target and a content scene, a ContentRoot with a
    // group and a preloaded group), TestsExtra (its own target and scene; the target references the root of TestsShared
    // directly) and TestsShared. For the active target (ContentTestBundles.Target) and with the type trees (the editor loads no bundle
    // without them), shipped as a player reads them in place, the catalog next to them. The assets stay in the project
    // until the run ends: Play Mode of the editor loads them. The build runs on a new empty scene (ContentTestScenes): a dirty
    // scene left by the tests before would stop it.
    public sealed class ContentBundleTestBuild : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup()
        {
            // First: a new scene also destroys the bundles made in memory before it.
            using var scenes = ContentTestScenes.Clean();

            EditorTestAssets.DeleteFolder(ContentTestPaths.Assets);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(ContentTestPaths.Assets));

            var shared = CreateAsset<ContentTestGroup>(ContentTestPaths.SharedAsset);
            var target = CreateTarget(ContentTestPaths.TargetAsset, null);
            var extraTarget = CreateTarget(ContentTestPaths.ExtraTargetAsset, shared);
            var references = CreateReferences(ContentTestPaths.ReferencesAsset, target, ContentTestPaths.SceneAsset);
            var extraReferences = CreateReferences(ContentTestPaths.ExtraReferencesAsset, extraTarget, ContentTestPaths.ExtraSceneAsset);
            var root = CreateContentRoot();
            AssetDatabase.SaveAssets();

            var bundles = new[]
            {
                ContentTestBundles.Create(ContentTestPaths.Name, references, root),
                ContentTestBundles.Create(ContentTestPaths.ExtraName, extraReferences),
                ContentTestBundles.Create(ContentTestPaths.SharedName, shared)
            };

            if (Directory.Exists(ContentTestPaths.Folder))
                Directory.Delete(ContentTestPaths.Folder, true);

            var platform = ContentTestBundles.Target;
            var built = ContentBundleBuilder.Build(ContentBundleLayout.Lay(bundles).Plans, platform, ContentTestPaths.Built, isTypeTree: true);
            ContentShipment.Write(built, platform, ContentTestPaths.Folder);
            File.WriteAllText(ContentTestPaths.Catalog, ContentCatalogWriter.Create(built, platform).ToJson());
        }

        public void Cleanup()
        {
            EditorTestAssets.DeleteFolder(ContentTestPaths.Assets);
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static ContentTestTarget CreateTarget(string path, Object direct)
        {
            var target = ScriptableObject.CreateInstance<ContentTestTarget>();
            target.Init(direct);
            AssetDatabase.CreateAsset(target, path);
            return target;
        }

        private static ContentTestReferences CreateReferences(string path, ContentTestTarget target, string scene)
        {
            File.WriteAllText(scene, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(scene, ImportAssetOptions.ForceSynchronousImport);

            var references = ScriptableObject.CreateInstance<ContentTestReferences>();
            references.Init(ContentReferenceFields.Create(target), ContentReferenceFields.CreateScene(scene));
            AssetDatabase.CreateAsset(references, path);
            return references;
        }

        // The ContentRoot of the group tests with a group and a preloaded group, written as ContentRootGenerator writes one.
        private static ContentRoot CreateContentRoot()
        {
            var group = CreateAsset<ContentTestGroup>(ContentTestPaths.GroupAsset);
            var preloaded = CreateAsset<ContentTestGroup>(ContentTestPaths.PreloadedGroupAsset);
            var root = CreateAsset<ContentRoot>(ContentTestPaths.ContentRootAsset);

            var serialized = new SerializedObject(root);
            AssignSingle(serialized.FindProperty("_groups"), group);
            AssignSingle(serialized.FindProperty("_preloadedGroups"), preloaded);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            return root;
        }

        private static void AssignSingle(SerializedProperty list, Object asset)
        {
            list.arraySize = 1;
            ContentReferenceFields.AssignObject(list.GetArrayElementAtIndex(0), asset);
        }
    }
}
