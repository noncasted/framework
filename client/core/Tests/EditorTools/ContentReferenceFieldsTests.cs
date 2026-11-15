using System.IO;
using Internal.EditorTests.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.EditorTests
{
    // The editor writes the GUID and the local id of a content reference and the GUID of a content scene, and reads their
    // targets back: what the drawers and the tools assign.
    public class ContentReferenceFieldsTests
    {
        private const string TempFolder = "Assets/ContentReferenceFieldsTemp";
        private const string TargetPath = TempFolder + "/Target.asset";
        private const string ScenePath = TempFolder + "/Level.unity";
        private const string ReferencesPath = TempFolder + "/References.asset";

        private ContentTestTarget _target;
        private ContentTestReferences _references;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            EditorTestAssets.DeleteFolder(TempFolder);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempFolder));
            File.WriteAllText(ScenePath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);

            _target = ScriptableObject.CreateInstance<ContentTestTarget>();
            AssetDatabase.CreateAsset(_target, TargetPath);
            _references = ScriptableObject.CreateInstance<ContentTestReferences>();
            AssetDatabase.CreateAsset(_references, ReferencesPath);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorTestAssets.DeleteFolder(TempFolder);
        }

        [Test]
        public void Create_KeysOfTheAsset()
        {
            var reference = ContentReferenceFields.Create(_target);
            var scene = ContentReferenceFields.CreateScene(ScenePath);

            AssertKeys(reference);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(ScenePath), scene.Guid);
            Assert.AreEqual(ScenePath, scene.EditorPath);
        }

        [Test]
        public void Assign_WritesKeys_NullClears()
        {
            using var serialized = new SerializedObject(_references);
            ContentReferenceFields.AssignObject(serialized.FindProperty("_target"), _target);
            ContentReferenceFields.AssignScene(serialized.FindProperty("_scene"), ScenePath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssertKeys(_references.Target);
            Assert.AreSame(_target, _references.Target.EditorAsset);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(ScenePath), _references.Scene.Guid);
            Assert.AreSame(_target, ContentReferenceFields.Target(serialized.FindProperty("_target")));
            Assert.AreEqual(ScenePath, AssetDatabase.GetAssetPath(ContentReferenceFields.TargetScene(serialized.FindProperty("_scene"))));

            ContentReferenceFields.AssignObject(serialized.FindProperty("_target"), null);
            ContentReferenceFields.AssignScene(serialized.FindProperty("_scene"), null);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(_references.Target.IsEmpty);
            Assert.AreEqual(string.Empty, _references.Target.Guid);
            Assert.AreEqual(0, _references.Target.LocalId);
            Assert.IsTrue(_references.Scene.IsEmpty);
            Assert.AreEqual(string.Empty, _references.Scene.Guid);
        }

        [Test]
        public void SceneThatIsNoScene_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => ContentReferenceFields.CreateScene(TargetPath));
        }

        private void AssertKeys(ContentReference<ContentTestTarget> reference)
        {
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_target, out var guid, out long localId));
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(TargetPath), reference.Guid);
            Assert.AreEqual(guid, reference.Guid);
            Assert.AreEqual(localId, reference.LocalId);
            Assert.AreNotEqual(0, reference.LocalId);
        }
    }
}
