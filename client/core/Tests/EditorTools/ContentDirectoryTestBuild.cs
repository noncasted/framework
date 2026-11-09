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
    // Builds the content directory of the Play Mode tests of the stores and the loader before Play Mode (registration is
    // refused in edit mode): a root with a lazy reference to a target and the id of a scene, a root with a content
    // reference and a content scene, the index and the GUID of the target next to it (ContentTestPaths). The assets stay
    // in the project until the run ends: Play Mode without a registered directory loads them.
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
            AssetDatabase.SaveAssets();

            if (Directory.Exists(ContentTestPaths.Output))
                Directory.Delete(ContentTestPaths.Output, true);

            var report = BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters
            {
                name = ContentTestPaths.Name,
                outputPath = ContentTestPaths.Folder,
                rootAssetPaths = new[] { ContentTestPaths.RootAsset, ContentTestPaths.ReferencesAsset },
                options = BuildContentOptions.None
            });

            Assert.That(report.summary.result, Is.EqualTo(BuildResult.Succeeded), "The build of the content directory of the store tests");

            var index = ContentIndexWriter.Write(ContentManifest.Read(ContentTestPaths.Folder), false);
            File.WriteAllText(ContentTestPaths.Index, index.ToJson());
            File.WriteAllText(ContentTestPaths.TargetGuid, AssetDatabase.AssetPathToGUID(ContentTestPaths.TargetAsset));
        }

        public void Cleanup()
        {
            EditorTestAssets.DeleteFolder(ContentTestPaths.Assets);
        }
    }
}
