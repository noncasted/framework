using System.IO;
using Internal.EditorTests.Content;
using NUnit.Framework;
using Unity.Loading;
using UnityEditor;
using UnityEngine;

namespace Internal.EditorTests
{
    // The editor writes the GUID and the local id of a content reference and the GUID of a content scene from their
    // engine ids: by an assignment of a tool and after the engine's own drawer changed the id.
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
            Assert.AreEqual(LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath), _references.Scene.Id);

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
        public void IdChangedByTheEngineDrawer_SyncFollows()
        {
            using var serialized = new SerializedObject(_references);
            var target = serialized.FindProperty("_target");
            var scene = serialized.FindProperty("_scene");

            // What the engine's drawers of Loadable and LoadableSceneId write.
            target.FindPropertyRelative("_loadable.m_LoadableObjectId").boxedValue = LoadableObjectIdEditorUtility.CreateLoadableObjectId(_target);
            scene.FindPropertyRelative("_id").boxedValue = LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath);

            Assert.IsTrue(ContentReferenceFields.SyncObject(target));
            Assert.IsTrue(ContentReferenceFields.SyncScene(scene));
            Assert.IsFalse(ContentReferenceFields.SyncObject(target), "In sync: nothing to write");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssertKeys(_references.Target);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(ScenePath), _references.Scene.Guid);
        }

        private void AssertKeys(ContentReference<ContentTestTarget> reference)
        {
            Assert.IsTrue(LoadableObjectIdEditorUtility.TryDeconstructLoadableObjectId(reference.Id, out var guid, out var localId, out _));
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(TargetPath), reference.Guid);
            Assert.AreEqual(guid.ToString(), reference.Guid);
            Assert.AreEqual(localId, reference.LocalId);
            Assert.AreNotEqual(0, reference.LocalId);
        }
    }
}
