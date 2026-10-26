using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Internal.EditorTests.Menu;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    // The two phases of the bindings (code, then references after the reload) on real prefab assets. The prefabs
    // live in the fixtures assembly (a runtime one: components of an editor assembly cannot be attached), so their
    // bindings go to Fixtures/Generated, where ShopBindings.g.cs is what the generator writes for a root named Shop:
    // the code does not change and nothing compiles during the tests.
    public class HierarchyBindingsBatchTests
    {
        private static string TempRoot => EditorTestPaths.Root + "/Fixtures/BindingsTemp";

        // No asmdef above and no script on the objects: no assembly to put the bindings into.
        private const string NoAssemblyRoot = "Assets/BindingsTempNoAssembly";

        private static string Fixture => EditorTestPaths.Root + "/Fixtures/Generated/ShopBindings.g.cs";
        private const string MenuNamespace = "Internal.EditorTests.Menu";
        private const string GamePlayNamespace = "Internal.EditorTests.GamePlay";

        [SetUp]
        public void SetUp()
        {
            EditorTestPaths.RequireAssets();
            ResetState();
        }

        [TearDown]
        public void TearDown()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                StageUtility.GoToMainStage();

            ResetState();
            File.SetAttributes(CatalogPaths.ToFullPath(Fixture), FileAttributes.Normal);

            foreach (var folder in new[] { TempRoot, NoAssemblyRoot })
            {
                if (AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.DeleteAsset(folder);
            }
        }

        // Menu/Shop.prefab and GamePlay/Shop.prefab, both bindings named ShopBindings: different classes, one file.
        [Test]
        public void TwoTypesOfOneFile_AreRefused()
        {
            var menu = MakePrefab(TempRoot + "/Menu/Shop.prefab", _ => { });
            var gamePlay = MakePrefab(TempRoot + "/GamePlay/Shop.prefab", _ => { });

            var items = new List<HierarchyBindingsBatch.PrefabItem>
            {
                Item(menu, MenuNamespace),
                Item(gamePlay, GamePlayNamespace)
            };

            LogAssert.Expect(LogType.Error, new Regex("ShopBindings"));
            Assert.That(HierarchyBindingsBatch.ValidateUniqueTypes(items), Is.False);
        }

        // The generated file of another class is not overwritten.
        [Test]
        public void FileOfAnotherNamespace_IsRefused()
        {
            var gamePlay = MakePrefab(TempRoot + "/GamePlay/Shop.prefab", _ => { });
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(gamePlay);

            // A refused write must not reach the fixture even when the check is missing.
            File.SetAttributes(CatalogPaths.ToFullPath(Fixture), FileAttributes.ReadOnly);

            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(MenuNamespace)));
            var prepared = HierarchyBindingsGenerator.Prepare(root, "ShopBindings", GamePlayNamespace, false, false,
                out _);

            Assert.That(prepared, Is.False);
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.True);
        }

        [Test]
        public void SiblingsWithTheSameName_TheBoundOneIsFound()
        {
            var path = MakePrefab(TempRoot + "/Window.prefab", root =>
            {
                Child(root, "Panel");
                Child(root, "Panel").AddComponent<ShopBindings>();
            });

            var items = new List<HierarchyBindingsBatch.PrefabItem>();
            HierarchyBindingsBatch.CollectPrefab(path, items);

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(HierarchyBindingsBatch.FindInAsset(items[0].Request).GetComponent<ShopBindings>(),
                Is.Not.Null);
        }

        [Test]
        public void NameWithSlash_IsFound()
        {
            var path = MakePrefab(TempRoot + "/Window.prefab", root =>
                Child(Child(root, "Left/Right"), "Inner").AddComponent<ShopBindings>());

            var items = new List<HierarchyBindingsBatch.PrefabItem>();
            HierarchyBindingsBatch.CollectPrefab(path, items);

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(HierarchyBindingsBatch.FindInAsset(items[0].Request).GetComponent<ShopBindings>(),
                Is.Not.Null);
        }

        // The object selected for a single Generate is found again after the reload.
        [Test]
        public void SiblingsWithTheSameName_TargetResolvesTheDescribedOne()
        {
            var path = MakePrefab(TempRoot + "/Window.prefab", root =>
            {
                Child(root, "Panel");
                Child(root, "Panel").AddComponent<ShopBindings>();
            });

            var described = AssetDatabase.LoadAssetAtPath<GameObject>(path).transform.GetChild(1).gameObject;
            Assert.That(HierarchyBindingsTarget.TryDescribe(described, out var assetPath, out var objectPath,
                out var isPrefab, out var error), Is.True, error);

            var request = new HierarchyBindingsRequest
            {
                AssetPath = assetPath,
                ObjectPath = objectPath,
                IsPrefabAsset = isPrefab
            };

            using var target = HierarchyBindingsTarget.Resolve(request, out error);

            Assert.That(target, Is.Not.Null, error);
            Assert.That(target.Root.GetComponent<ShopBindings>(), Is.Not.Null);
        }

        // A binding that throws ends the batch and leaves nothing pending.
        [Test]
        public void BindingThrows_BatchIsReset()
        {
            var shop = MakePrefab(TempRoot + "/Menu/Shop.prefab", root => root.AddComponent<ShopBindings>());

            HierarchyBindingsBatch.RegeneratePrefabs(new[] { shop });
            Assert.That(HierarchyBindingsBatch.IsRunning, Is.True);

            HierarchyBindingsQueue.Enqueue(new HierarchyBindingsRequest
            {
                AssetPath = Fixture,
                ObjectPath = string.Empty,
                TypeName = nameof(ShopBindings),
                Namespace = MenuNamespace,
                IsPrefabAsset = true,
                StructureHash = "stale"
            });

            Assert.Catch<Exception>(() => HierarchyBindingsGenerator.PumpPending());
            Assert.That(HierarchyBindingsBatch.Status(), Is.EqualTo("Idle."));
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.True);
        }

        // A phase that throws while writing its code ends the batch too.
        [Test]
        public void PhaseThrows_BatchIsReset()
        {
            var lonely = MakePrefab(NoAssemblyRoot + "/Lonely.prefab", root => root.AddComponent<ShopBindings>());

            Assert.Catch<Exception>(() => HierarchyBindingsBatch.RegeneratePrefabs(new[] { lonely }));
            Assert.That(HierarchyBindingsBatch.Status(), Is.EqualTo("Idle."));
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.True);
        }

        // The request is queued only once its code is written: otherwise it waits for a reload that never comes.
        [Test]
        public void FailedWrite_NothingIsQueued()
        {
            var shop = MakePrefab(TempRoot + "/Menu/Shop.prefab", root => Child(root, "Title"));
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(shop);
            File.SetAttributes(CatalogPaths.ToFullPath(Fixture), FileAttributes.ReadOnly);

            LogAssert.ignoreFailingMessages = true;
            Assert.Catch<Exception>(() =>
                HierarchyBindingsGenerator.Prepare(root, "ShopBindings", MenuNamespace, false, false, out _));
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.True);
        }

        // Stuck requests outside a batch can be dropped from the menu too.
        [Test]
        public void PendingRequestWithoutBatch_CanBeReset()
        {
            HierarchyBindingsQueue.Enqueue(new HierarchyBindingsRequest { AssetPath = Fixture, TypeName = "X" });

            Assert.That(HierarchyBindingsMenu.ValidateCancelBatch(), Is.True);
        }

        // ProjectVersion.txt is a file, so no folder can be created under it.
        [Test]
        public void FailedWrite_Throws()
        {
            Assert.Catch<Exception>(() => GeneratedFile.WriteIfChanged("HierarchyBindingsBatchTests",
                "ProjectSettings/ProjectVersion.txt/X.cs", "x"));
        }

        // A queue that cannot be read is an error, not an empty queue: its requests would be lost silently.
        [Test]
        public void CorruptQueue_Throws()
        {
            SessionState.SetString("Internal.HierarchyBindings.PendingRequests", "{ corrupt");

            Assert.Catch<Exception>(() => HierarchyBindingsQueue.IsEmpty());
        }

        // The prefab stage edits a copy of the asset: a batch that reads one prefab from the stage and the others
        // from the disk mixes two versions.
        [Test]
        public void OpenPrefabStage_BatchIsRefused()
        {
            var shop = MakePrefab(TempRoot + "/Menu/Shop.prefab", root => root.AddComponent<ShopBindings>());
            var stage = PrefabStageUtility.OpenPrefab(shop);

            Assert.That(stage, Is.Not.Null, "The prefab stage did not open.");
            Assert.Throws<InvalidOperationException>(() => HierarchyBindingsBatch.RegeneratePrefabs(new[] { shop }));
            Assert.That(HierarchyBindingsBatch.IsRunning, Is.False);
        }

        // A duplicated prefab (Ctrl+D) keeps the layer GUID of the original; its bindings are still its own.
        [Test]
        public void DuplicatedPrefab_BindingsAreOwn()
        {
            var original = MakePrefab(TempRoot + "/Original.prefab", root => root.AddComponent<ShopBindings>());
            var bindings = AssetDatabase.LoadAssetAtPath<GameObject>(original).GetComponent<ShopBindings>();
            var serialized = new SerializedObject(bindings);
            serialized.FindProperty("_layerAssetGuid").stringValue = AssetDatabase.AssetPathToGUID(original);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            var copy = TempRoot + "/Copy.prefab";
            Assert.That(AssetDatabase.CopyAsset(original, copy), Is.True);

            var copied = AssetDatabase.LoadAssetAtPath<GameObject>(copy).GetComponent<ShopBindings>();

            Assert.That(HierarchyBindingsWindow.IsForeignLayer(copied, copy), Is.False);
        }

        private static HierarchyBindingsBatch.PrefabItem Item(string assetPath, string namespaceName)
        {
            return new HierarchyBindingsBatch.PrefabItem
            {
                FullTypeName = namespaceName + ".ShopBindings",
                Request = new HierarchyBindingsRequest
                {
                    AssetPath = assetPath,
                    ObjectPath = string.Empty,
                    TypeName = "ShopBindings",
                    Namespace = namespaceName,
                    IsPrefabAsset = true
                }
            };
        }

        private static string MakePrefab(string path, Action<GameObject> build)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            var root = new GameObject(Path.GetFileNameWithoutExtension(path));

            try
            {
                build(root);
                PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
                Assert.That(success, Is.True, path);
                return path;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            EnsureFolder(Path.GetDirectoryName(folder).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }

        private static void ResetState()
        {
            SessionState.EraseString("Internal.HierarchyBindings.PrefabBatch");
            SessionState.EraseString("Internal.HierarchyBindings.PendingRequests");
            SessionState.EraseBool("Internal.HierarchyBindings.AwaitingReload");
        }
    }
}
