using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Internal.EditorTests.Content;
using NUnit.Framework;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Internal.EditorTests
{
    // A real content directory built from the assets of the test: a root with a Loadable of an asset and the id of a
    // scene, the asset with a direct reference to a clip. The index must give each load its own files only.
    public class ContentIndexWriterTests
    {
        private const string TempFolder = "Assets/ContentIndexTemp";
        private const string ClipPath = TempFolder + "/Tone.wav";
        private const string TargetPath = TempFolder + "/Target.asset";
        private const string ScenePath = TempFolder + "/Level.unity";
        private const string RootPath = TempFolder + "/Root.asset";
        private const string Output = "Temp/ContentIndexTests";
        private const string Name = "Tests";

        private ContentManifest _manifest;
        private ContentIndex _index;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // A crash of the editor in a test leaves the folder, and CreateFolder would make "... 1".
            EditorTestAssets.DeleteFolder(TempFolder);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempFolder));

            File.WriteAllBytes(ClipPath, Wave());
            File.WriteAllText(ScenePath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);

            var target = ScriptableObject.CreateInstance<ContentTestTarget>();
            target.Init(AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath));
            AssetDatabase.CreateAsset(target, TargetPath);

            var root = ScriptableObject.CreateInstance<ContentTestRoot>();
            root.Init(LoadableObjectIdEditorUtility.CreateLoadableObjectId(target), LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath));
            AssetDatabase.CreateAsset(root, RootPath);
            AssetDatabase.SaveAssets();

            if (Directory.Exists(Output))
                Directory.Delete(Output, true);

            var report = BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters
            {
                name = Name,
                outputPath = $"{Output}/{Name}",
                rootAssetPaths = new[] { RootPath },
                options = BuildContentOptions.None
            });

            Assert.That(report.summary.result, Is.EqualTo(BuildResult.Succeeded), "The build of the content directory");

            _manifest = ContentManifest.Read($"{Output}/{Name}");
            _index = ContentIndexWriter.Write(_manifest, false);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorTestAssets.DeleteFolder(TempFolder);

            if (Directory.Exists(Output))
                Directory.Delete(Output, true);
        }

        [Test]
        public void Index_ListsEveryFileOfTheFolderWithItsSize()
        {
            var folder = $"{Output}/{Name}";
            var expected = Directory.GetFiles(folder)
                .Select(path => new ContentFile(Path.GetFileName(path), new FileInfo(path).Length))
                .Where(file => file.Name != ContentManifest.HashFile)
                .OrderBy(file => file.Name, StringComparer.Ordinal);

            Assert.That(_index.Name, Is.EqualTo(Name));
            Assert.That(_index.ManifestHash, Is.EqualTo(File.ReadAllText($"{folder}/{ContentManifest.HashFile}").Trim()));
            Assert.That(_index.Manifest.Name, Is.EqualTo($"{_index.ManifestHash}.json"));
            Assert.That(_index.Files, Is.EqualTo(expected));
        }

        [Test]
        public void RootFiles_AreTheRootAndItsDependenciesWithoutTheTargets()
        {
            var root = ObjectFiles(AssetDatabase.LoadAssetAtPath<ContentTestRoot>(RootPath));
            var target = ObjectFiles(AssetDatabase.LoadAssetAtPath<ContentTestTarget>(TargetPath));
            var scene = SceneFiles();

            Assert.That(_index.RootFiles, Is.EqualTo(root));
            Assert.That(_index.RootFiles, Has.No.Member(target[0]), "The file of the Loadable target");
            Assert.That(_index.RootFiles, Has.No.Member(scene[0]), "The file of the scene");
            Assert.That(_index.RootFiles.Select(file => file.Name), Has.All.EndsWith(".cf"), "Resources of the root");
        }

        [Test]
        public void AssetFiles_StartWithItsFileAndHoldTheResourceOfItsClip()
        {
            var target = ObjectFiles(AssetDatabase.LoadAssetAtPath<ContentTestTarget>(TargetPath));
            var clip = ObjectFiles(AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath));
            var resources = clip.Where(file => file.Name.EndsWith(".cf") == false).ToArray();

            Assert.That(target[0].Name, Does.EndWith(".cf"));
            Assert.That(resources, Has.Length.EqualTo(1), $"The resource of the clip among {string.Join(", ", clip)}");
            Assert.That(target, Is.SupersetOf(clip), "The clip of a direct reference");
            Assert.That(_index.RootFiles, Has.No.Member(resources[0]));
        }

        [Test]
        public void SceneFiles_StartWithTheSceneFileAndHoldNoTarget()
        {
            var scene = SceneFiles();
            var target = ObjectFiles(AssetDatabase.LoadAssetAtPath<ContentTestTarget>(TargetPath));

            Assert.That(scene[0].Name, Does.EndWith(".cf"));
            Assert.That(scene, Has.No.Member(target[0]));
            Assert.That(_index.Scenes.Single().Path, Is.EqualTo(ScenePath));
        }

        [Test]
        public void Json_GivesTheSameIndexBack()
        {
            var read = ContentIndex.FromJson(_index.ToJson());
            var target = AssetDatabase.AssetPathToGUID(TargetPath);

            Assert.That(read.Name, Is.EqualTo(_index.Name));
            Assert.That(read.ManifestHash, Is.EqualTo(_index.ManifestHash));
            Assert.That(read.Files, Is.EqualTo(_index.Files));
            Assert.That(read.RootFiles, Is.EqualTo(_index.RootFiles));
            Assert.That(read.ObjectFiles(target, 0), Is.EqualTo(_index.ObjectFiles(target, 0)));
            Assert.That(read.SceneFiles(SceneGuid()), Is.EqualTo(_index.SceneFiles(SceneGuid())));
        }

        [Test]
        public void Read_ThrowsOnAnUnknownVersion()
        {
            var folder = $"{Output}/Broken";
            Directory.CreateDirectory(folder);
            File.Copy($"{Output}/{Name}/{ContentManifest.HashFile}", $"{folder}/{ContentManifest.HashFile}", true);
            var json = File.ReadAllText($"{Output}/{Name}/{_manifest.FileName}");
            File.WriteAllText($"{folder}/{_manifest.FileName}", Regex.Replace(json, "\"Version\"\\s*:\\s*3", "\"Version\": 99"));

            Assert.Throws<InvalidDataException>(() => ContentManifest.Read(folder));
        }

        [Test]
        public void ObjectFiles_ThrowsOnAnObjectOutsideTheDirectory()
        {
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => _index.ObjectFiles("0123456789abcdef0123456789abcdef", 1));
        }

        private System.Collections.Generic.IReadOnlyList<ContentFile> ObjectFiles(UnityEngine.Object asset)
        {
            var id = LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset);
            Assert.That(LoadableObjectIdEditorUtility.TryDeconstructLoadableObjectId(id, out var guid, out var localId, out _), Is.True);
            return _index.ObjectFiles(guid.ToString(), localId);
        }

        private System.Collections.Generic.IReadOnlyList<ContentFile> SceneFiles()
        {
            return _index.SceneFiles(SceneGuid());
        }

        private static string SceneGuid()
        {
            return LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath).ToString();
        }

        // A tenth of a second of a 440 Hz tone, 16-bit PCM mono.
        private static byte[] Wave()
        {
            const int rate = 8000;
            const int samples = rate / 10;

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.ASCII);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(rate);
            writer.Write(rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(samples * 2);

            for (var i = 0; i < samples; i++)
                writer.Write((short)(Math.Sin(2 * Math.PI * 440 * i / rate) * short.MaxValue * 0.5));

            writer.Flush();
            return stream.ToArray();
        }
    }
}
