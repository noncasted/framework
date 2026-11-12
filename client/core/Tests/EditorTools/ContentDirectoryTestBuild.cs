using System.IO;
using Internal.EditorTests.Content;
using NUnit.Framework;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.EditorTests
{
    // Builds the content directory of the Play Mode tests of the stores, the loader and the catalogue groups before Play
    // Mode (registration is refused in edit mode): a root with a lazy reference to a target and the id of a scene, a root
    // with a content reference and a content scene, a ContentRoot with a group and a preloaded group, its archive and index as a player reads them in place,
    // a copy of the index and the GUID of the target next to it (ContentTestPaths), and a second directory of its own assets. The assets stay in the project until the run ends: Play Mode
    // without a registered directory loads them.
    public sealed class ContentDirectoryTestBuild : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup()
        {
            EditorTestAssets.DeleteFolder(ContentTestPaths.Assets);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(ContentTestPaths.Assets));

            File.WriteAllText(ContentTestPaths.SceneAsset, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(ContentTestPaths.SceneAsset, ImportAssetOptions.ForceSynchronousImport);

            var target = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(target, ContentTestPaths.TargetAsset);

            var root = ScriptableObject.CreateInstance<ContentTestRoot>();
            root.Init(LoadableObjectIdEditorUtility.CreateLoadableObjectId(target),
                LoadableSceneIdEditorUtility.CreateLoadableSceneId(ContentTestPaths.SceneAsset));
            AssetDatabase.CreateAsset(root, ContentTestPaths.RootAsset);

            var references = ScriptableObject.CreateInstance<ContentTestReferences>();
            references.Init(ContentReferenceFields.Create(target), ContentReferenceFields.CreateScene(ContentTestPaths.SceneAsset));
            AssetDatabase.CreateAsset(references, ContentTestPaths.ReferencesAsset);
            CreateContentRoot();
            AssetDatabase.SaveAssets();

            if (Directory.Exists(ContentTestPaths.Output))
                Directory.Delete(ContentTestPaths.Output, true);

            var report = BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters
            {
                name = ContentTestPaths.Name,
                outputPath = ContentTestPaths.Built,
                rootAssetPaths = new[] { ContentTestPaths.RootAsset, ContentTestPaths.ReferencesAsset, ContentTestPaths.ContentRootAsset },
                options = BuildContentOptions.None
            });

            Assert.That(report.summary.result, Is.EqualTo(BuildResult.Succeeded), "The build of the content directory of the store tests");

            // With the type trees, unlike a player build: the editor registers no directory without them.
            var manifest = ContentManifest.Read(ContentTestPaths.Built);
            var index = ContentIndexWriter.Write(manifest, ContentArchiveWriter.Write(manifest, new ContentLoadKey[0], ContentTestPaths.Folder));
            ContentShipment.Write(index, ContentTestPaths.Folder, false, false);
            File.WriteAllText(ContentTestPaths.Index, index.ToJson());
            File.WriteAllText(ContentTestPaths.TargetGuid, AssetDatabase.AssetPathToGUID(ContentTestPaths.TargetAsset));

            BuildExtra();
        }

        // The second directory: its own target and scene, nothing of the first one.
        private static void BuildExtra()
        {
            File.WriteAllText(ContentTestPaths.ExtraSceneAsset, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(ContentTestPaths.ExtraSceneAsset, ImportAssetOptions.ForceSynchronousImport);

            var target = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(target, ContentTestPaths.ExtraTargetAsset);

            var references = ScriptableObject.CreateInstance<ContentTestReferences>();
            references.Init(ContentReferenceFields.Create(target), ContentReferenceFields.CreateScene(ContentTestPaths.ExtraSceneAsset));
            AssetDatabase.CreateAsset(references, ContentTestPaths.ExtraReferencesAsset);
            AssetDatabase.SaveAssets();

            var report = BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters
            {
                name = ContentTestPaths.ExtraName,
                outputPath = ContentTestPaths.ExtraBuilt,
                rootAssetPaths = new[] { ContentTestPaths.ExtraReferencesAsset },
                options = BuildContentOptions.None
            });

            Assert.That(report.summary.result, Is.EqualTo(BuildResult.Succeeded), "The build of the extra content directory");

            var manifest = ContentManifest.Read(ContentTestPaths.ExtraBuilt);
            var index = ContentIndexWriter.Write(manifest, ContentArchiveWriter.Write(manifest, new ContentLoadKey[0], ContentTestPaths.ExtraFolder));
            ContentShipment.Write(index, ContentTestPaths.ExtraFolder, false, false);
            File.WriteAllText(ContentTestPaths.ExtraIndex, index.ToJson());
        }

        public void Cleanup()
        {
            EditorTestAssets.DeleteFolder(ContentTestPaths.Assets);
        }

        // The ContentRoot of the group tests with a group and a preloaded group, written as ContentRootGenerator writes one.
        private static void CreateContentRoot()
        {
            var group = ScriptableObject.CreateInstance<ContentTestGroup>();
            AssetDatabase.CreateAsset(group, ContentTestPaths.GroupAsset);

            var preloaded = ScriptableObject.CreateInstance<ContentTestGroup>();
            AssetDatabase.CreateAsset(preloaded, ContentTestPaths.PreloadedGroupAsset);

            var root = ScriptableObject.CreateInstance<ContentRoot>();
            AssetDatabase.CreateAsset(root, ContentTestPaths.ContentRootAsset);

            var serialized = new SerializedObject(root);
            AssignSingle(serialized.FindProperty("_groups"), group);
            AssignSingle(serialized.FindProperty("_preloadedGroups"), preloaded);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
        }

        private static void AssignSingle(SerializedProperty list, Object asset)
        {
            list.arraySize = 1;
            ContentReferenceFields.AssignObject(list.GetArrayElementAtIndex(0), asset);
        }
    }
}
