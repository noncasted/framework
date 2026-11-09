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
    // Builds the content directory of the Play Mode tests of the stores before Play Mode (registration is refused in
    // edit mode): a root with a lazy reference to a target and the id of a scene, the index and the GUID of the target
    // next to it (ContentTestPaths). The assets are deleted after the build: the directory holds its own copies.
    public sealed class ContentDirectoryTestBuild : IPrebuildSetup
    {
        private const string TempFolder = "Assets/ContentDirectoryTestsTemp";
        private const string TargetPath = TempFolder + "/Target.asset";
        private const string ScenePath = TempFolder + "/Level.unity";
        private const string RootPath = TempFolder + "/Root.asset";

        public void Setup()
        {
            EditorTestAssets.DeleteFolder(TempFolder);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempFolder));

            try
            {
                File.WriteAllText(ScenePath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);

                var target = ScriptableObject.CreateInstance<ContentTestTarget>();
                AssetDatabase.CreateAsset(target, TargetPath);

                var root = ScriptableObject.CreateInstance<ContentTestRoot>();
                root.Init(LoadableObjectIdEditorUtility.CreateLoadableObjectId(target), LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath));
                AssetDatabase.CreateAsset(root, RootPath);
                AssetDatabase.SaveAssets();

                if (Directory.Exists(ContentTestPaths.Output))
                    Directory.Delete(ContentTestPaths.Output, true);

                var report = BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters
                {
                    name = ContentTestPaths.Name,
                    outputPath = ContentTestPaths.Folder,
                    rootAssetPaths = new[] { RootPath },
                    options = BuildContentOptions.None
                });

                Assert.That(report.summary.result, Is.EqualTo(BuildResult.Succeeded), "The build of the content directory of the store tests");

                var index = ContentIndexWriter.Write(ContentManifest.Read(ContentTestPaths.Folder), false);
                File.WriteAllText(ContentTestPaths.Index, index.ToJson());
                File.WriteAllText(ContentTestPaths.TargetGuid, AssetDatabase.AssetPathToGUID(TargetPath));
            }
            finally
            {
                EditorTestAssets.DeleteFolder(TempFolder);
            }
        }
    }
}
