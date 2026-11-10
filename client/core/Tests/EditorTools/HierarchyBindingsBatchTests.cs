using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Internal.EditorTests.Menu;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

            EditorTestAssets.DeleteFiles(TempRoot);
            EditorTestAssets.DeleteFolder(NoAssemblyRoot);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorTestAssets.DeleteFolder(TempRoot);
        }

        // Menu/Shop.prefab and GamePlay/Shop.prefab, both bindings named ShopBindings: different classes, one file.
        [Test]
        public void TwoTypesOfOneFile_AreRefused()
        {
            var menu = MakePrefab(TempRoot + "/Menu/Shop.prefab", _ => { });
            var gamePlay = MakePrefab(TempRoot + "/GamePlay/Shop.prefab", _ => { });

            var items = new List<HierarchyBindingsPrefabItem>
            {
                Item(menu, MenuNamespace),
                Item(gamePlay, GamePlayNamespace)
            };

            LogAssert.Expect(LogType.Error, new Regex("ShopBindings"));
            Assert.That(HierarchyBindingsPrefabItems.ValidateUniqueTypes(items), Is.False);
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

            var items = new List<HierarchyBindingsPrefabItem>();
            HierarchyBindingsPrefabItems.CollectPrefab(path, items);

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(HierarchyBindingsPrefabItems.FindInAsset(items[0].Request).GetComponent<ShopBindings>(),
                Is.Not.Null);
        }

        [Test]
        public void NameWithSlash_IsFound()
        {
            var path = MakePrefab(TempRoot + "/Window.prefab", root =>
                Child(Child(root, "Left/Right"), "Inner").AddComponent<ShopBindings>());

            var items = new List<HierarchyBindingsPrefabItem>();
            HierarchyBindingsPrefabItems.CollectPrefab(path, items);

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(HierarchyBindingsPrefabItems.FindInAsset(items[0].Request).GetComponent<ShopBindings>(),
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

        // Every operation of the queue goes through its json in SessionState, so this is the round trip a domain
        // reload relies on: each Enqueue reads the earlier requests back and Load reads all of them.
        [Test]
        public void EnqueuedRequests_AreReadBackInOrder()
        {
            Assert.That(HierarchyBindingsQueue.HasData(), Is.False);
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.True);

            HierarchyBindingsQueue.Enqueue(new HierarchyBindingsRequest
            {
                AssetPath = "Assets/First.prefab",
                ObjectPath = "1/0",
                ObjectNames = "Root/Panel",
                TypeName = "FirstBindings",
                Namespace = MenuNamespace,
                IsPrefabAsset = true,
                IsSceneService = false,
                IsEntityComponent = true,
                StructureHash = "first-hash"
            });

            HierarchyBindingsQueue.Enqueue(new HierarchyBindingsRequest
            {
                AssetPath = "Assets/Second.unity",
                ObjectPath = string.Empty,
                ObjectNames = "Scene",
                TypeName = "SecondBindings",
                Namespace = GamePlayNamespace,
                IsPrefabAsset = false,
                IsSceneService = true,
                IsEntityComponent = false,
                StructureHash = "second-hash"
            });

            Assert.That(HierarchyBindingsQueue.HasData(), Is.True);
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.False);

            var requests = HierarchyBindingsQueue.Load();
            Assert.That(requests, Has.Count.EqualTo(2));

            var first = requests[0];
            Assert.That(first.AssetPath, Is.EqualTo("Assets/First.prefab"));
            Assert.That(first.ObjectPath, Is.EqualTo("1/0"));
            Assert.That(first.ObjectNames, Is.EqualTo("Root/Panel"));
            Assert.That(first.TypeName, Is.EqualTo("FirstBindings"));
            Assert.That(first.Namespace, Is.EqualTo(MenuNamespace));
            Assert.That(first.IsPrefabAsset, Is.True);
            Assert.That(first.IsSceneService, Is.False);
            Assert.That(first.IsEntityComponent, Is.True);
            Assert.That(first.StructureHash, Is.EqualTo("first-hash"));

            var second = requests[1];
            Assert.That(second.AssetPath, Is.EqualTo("Assets/Second.unity"));
            Assert.That(second.ObjectPath, Is.Empty);
            Assert.That(second.ObjectNames, Is.EqualTo("Scene"));
            Assert.That(second.TypeName, Is.EqualTo("SecondBindings"));
            Assert.That(second.Namespace, Is.EqualTo(GamePlayNamespace));
            Assert.That(second.IsPrefabAsset, Is.False);
            Assert.That(second.IsSceneService, Is.True);
            Assert.That(second.IsEntityComponent, Is.False);
            Assert.That(second.StructureHash, Is.EqualTo("second-hash"));

            HierarchyBindingsQueue.Clear();

            Assert.That(HierarchyBindingsQueue.HasData(), Is.False);
            Assert.That(HierarchyBindingsQueue.Load(), Is.Empty);
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

        // The whole batch on a prefab whose code does not change: the phase binds without a compile, the plan ends.
        [Test]
        public void RegeneratePrefabs_BindsThePhaseAndEnds()
        {
            var shop = MakePrefab(TempRoot + "/Menu/Shop.prefab", root => root.AddComponent<ShopBindings>());

            var status = HierarchyBindingsBatch.RegeneratePrefabs(new[] { shop, shop });

            Assert.That(status, Is.EqualTo("Phase 1 of 1 is binding."));
            Assert.That(HierarchyBindingsBatch.IsRunning, Is.True);
            Assert.That(HierarchyBindingsGenerator.HasPending(), Is.True);

            Assert.That(HierarchyBindingsGenerator.PumpPending(), Is.EqualTo("Idle."));

            var bindings = AssetDatabase.LoadAssetAtPath<GameObject>(shop).GetComponent<ShopBindings>();
            Assert.That(HierarchyBindingsBatch.IsRunning, Is.False);
            Assert.That(HierarchyBindingsGenerator.HasPending(), Is.False);
            Assert.That(bindings.StructureHash, Is.EqualTo("9809dfaf8e9b1aa55835ef9f03679833"));
            Assert.That(bindings.Transform, Is.EqualTo(bindings.transform));
            Assert.That(bindings.LayerAssetGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(shop)));
        }

        [Test]
        public void RegeneratePrefabs_TypeOnTwoObjects_NothingRuns()
        {
            var first = MakePrefab(TempRoot + "/First.prefab", root => root.AddComponent<ShopBindings>());
            var second = MakePrefab(TempRoot + "/Second.prefab", root => root.AddComponent<ShopBindings>());

            LogAssert.Expect(LogType.Error, new Regex("declared on more than one object"));
            var result = HierarchyBindingsBatch.RegeneratePrefabs(new[] { first, second });

            Assert.That(result, Is.EqualTo("Duplicate bindings types. See the console."));
            Assert.That(HierarchyBindingsBatch.IsRunning, Is.False);
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.True);
        }

        [Test]
        public void RegeneratePrefabs_QueueNotEmpty_IsRefused()
        {
            HierarchyBindingsQueue.Enqueue(new HierarchyBindingsRequest { AssetPath = Fixture, TypeName = "X" });

            Assert.Throws<InvalidOperationException>(() => HierarchyBindingsBatch.RegeneratePrefabs(new string[0]));
        }

        [Test]
        public void RegeneratePrefabs_NoBindings_EndsAtOnce()
        {
            var plain = MakePrefab(TempRoot + "/Plain.prefab", _ => { });

            Assert.That(HierarchyBindingsBatch.RegeneratePrefabs(new[] { plain }), Is.EqualTo("Idle."));
        }

        [Test]
        public void Cancel_DropsThePlanAndTheRequests()
        {
            var shop = MakePrefab(TempRoot + "/Menu/Shop.prefab", root => root.AddComponent<ShopBindings>());
            HierarchyBindingsBatch.RegeneratePrefabs(new[] { shop });

            HierarchyBindingsBatch.Cancel();

            Assert.That(HierarchyBindingsBatch.Status(), Is.EqualTo("Idle."));
            Assert.That(HierarchyBindingsGenerator.HasPending(), Is.False);
        }

        [Test]
        public void PumpPending_AwaitingReload_DoesNotBind()
        {
            HierarchyBindingsQueue.Enqueue(new HierarchyBindingsRequest { AssetPath = Fixture, TypeName = "X" });
            SessionState.SetBool("Internal.HierarchyBindings.AwaitingReload", true);

            Assert.That(HierarchyBindingsGenerator.PumpPending(), Is.EqualTo("Waiting for compilation. Idle."));
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.False);

            HierarchyBindingsGenerator.ResetPending();
            Assert.That(HierarchyBindingsGenerator.HasPending(), Is.False);
        }

        [Test]
        public void GeneratedType_IsFoundUnderTheUserClass()
        {
            var root = new GameObject("Shop");

            try
            {
                var type = HierarchyBindingsGenerator.ResolveGeneratedType(root.AddComponent<UserShopBindings>());

                Assert.That(type, Is.EqualTo(typeof(ShopBindings)));
                Assert.That(HierarchyBindingsGenerator.ResolveGeneratedType(null), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("Shop", "ShopBindings")]
        [TestCase("ShopBindings", "ShopBindings")]
        [TestCase("my shop", "MyShopBindings")]
        [TestCase("!!!", "")]
        public void TypeName_FromTheBindingsName(string bindingsName, string expected)
        {
            Assert.That(HierarchyBindingsGenerator.ToTypeName(bindingsName), Is.EqualTo(expected));
        }

        // The documented way to bind from MCP while the editor is unfocused (HIERARCHY_BINDINGS.md).
        [Test]
        public void ProcessPending_IsReachableByReflection()
        {
            var method = typeof(HierarchyBindingsGenerator)
                .GetMethod("ProcessPending", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
        }

        [Test]
        public void Prepare_NotAnIdentifier_IsRefused()
        {
            var shop = MakePrefab(TempRoot + "/Menu/Shop.prefab", _ => { });
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(shop);

            LogAssert.Expect(LogType.Error, new Regex("is not a C# identifier"));

            Assert.That(HierarchyBindingsGenerator.Prepare(root, "!!!", MenuNamespace, false, false, out _), Is.False);
        }

        // Without bindUnchanged the same code binds again only for a stale object.
        [Test]
        public void Prepare_SameCode_BindsOnlyAStaleObject()
        {
            var shop = MakePrefab(TempRoot + "/Menu/Shop.prefab", root => root.AddComponent<ShopBindings>());
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(shop);

            Assert.That(HierarchyBindingsGenerator.Prepare(asset, "ShopBindings", MenuNamespace, false, false,
                out var changed, false), Is.True);
            Assert.That(changed, Is.False);
            Assert.That(HierarchyBindingsQueue.Load(), Has.Count.EqualTo(1));

            HierarchyBindingsGenerator.PumpPending();
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.True);

            asset = AssetDatabase.LoadAssetAtPath<GameObject>(shop);
            Assert.That(HierarchyBindingsGenerator.Prepare(asset, "ShopBindings", MenuNamespace, false, false,
                out changed, false), Is.True);
            Assert.That(changed, Is.False);
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.True);
        }

        [Test]
        public void Scene_CountAndRegenerate()
        {
            Assert.That(HierarchyBindingsBatch.Count(default), Is.EqualTo(0));
            Assert.That(HierarchyBindingsBatch.RegenerateScene(default), Is.EqualTo("No loaded scene."));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            try
            {
                Assert.That(HierarchyBindingsBatch.RegenerateScene(scene), Does.StartWith("No bindings found"));

                var root = new GameObject("Shop");
                root.AddComponent<ShopBindings>();
                SceneManager.MoveGameObjectToScene(root, scene);

                EnsureFolder(TempRoot);
                Assert.That(EditorSceneManager.SaveScene(scene, TempRoot + "/Scene.unity"), Is.True);
                Assert.That(HierarchyBindingsBatch.Count(scene), Is.EqualTo(1));

                var result = HierarchyBindingsBatch.RegenerateScene(scene);

                Assert.That(result, Is.EqualTo("Regenerating 1 bindings in 'Scene'..."));
                Assert.That(HierarchyBindingsQueue.Load().Single().AssetPath, Is.EqualTo(TempRoot + "/Scene.unity"));
            }
            finally
            {
                // The only scene cannot be closed, and its file goes away with TempRoot.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        // One class - one object, in a scene the second one is skipped loudly.
        [Test]
        public void Scene_TypeOnTwoObjects_TheSecondIsSkipped()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            try
            {
                foreach (var name in new[] { "Shop", "Other" })
                {
                    var root = new GameObject(name);
                    root.AddComponent<ShopBindings>();
                    SceneManager.MoveGameObjectToScene(root, scene);
                }

                EnsureFolder(TempRoot);
                Assert.That(EditorSceneManager.SaveScene(scene, TempRoot + "/Scene.unity"), Is.True);

                LogAssert.Expect(LogType.Error, new Regex("is used by more than one object"));

                Assert.That(HierarchyBindingsBatch.RegenerateScene(scene), Does.StartWith("Regenerating 1 bindings"));
            }
            finally
            {
                // The only scene cannot be closed, and its file goes away with TempRoot.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void PrepareProject_QueueNotEmpty_IsRefused()
        {
            HierarchyBindingsQueue.Enqueue(new HierarchyBindingsRequest { AssetPath = Fixture, TypeName = "X" });

            Assert.Throws<InvalidOperationException>(() => HierarchyBindingsBatch.PrepareProject(out _));
        }

        [Test]
        public void PrepareProject_StaleObject_IsQueuedWithoutACompile()
        {
            var shop = MakePrefab(TempRoot + "/Menu/Shop.prefab", root => root.AddComponent<ShopBindings>());

            var prepared = HierarchyBindingsBatch.PrepareProject(out var changed);

            Assert.That(prepared, Is.EqualTo(1));
            Assert.That(changed, Is.False);
            Assert.That(HierarchyBindingsQueue.Load().Single().AssetPath, Is.EqualTo(shop));
            Assert.That(HierarchyBindingsBatch.IsRunning, Is.False);
        }

        [Test]
        public void PrepareProject_TypeOnTwoObjects_Throws()
        {
            MakePrefab(TempRoot + "/First.prefab", root => root.AddComponent<ShopBindings>());
            MakePrefab(TempRoot + "/Second.prefab", root => root.AddComponent<ShopBindings>());

            LogAssert.Expect(LogType.Error, new Regex("declared on more than one object"));

            Assert.Throws<InvalidOperationException>(() => HierarchyBindingsBatch.PrepareProject(out _));
        }

        // A variant declares only what it added: the bindings of the base stay in the base, and they plan deeper.
        [Test]
        public void Variant_CollectsOnlyItsOwnBindings()
        {
            var (_, variant) = MakeVariant(added => added.AddComponent<ExtraBindings>());

            var items = new List<HierarchyBindingsPrefabItem>();
            HierarchyBindingsPrefabItems.CollectPrefab(variant, items);

            var item = items.Single();
            Assert.That(item.FullTypeName, Is.EqualTo(typeof(ExtraBindings).FullName));
            Assert.That(item.VariantDepth, Is.EqualTo(1));
            Assert.That(item.HierarchyDepth, Is.EqualTo(2));
        }

        // The layer of a variant: what it added, reached through a pass-through node without a reference to the object.
        [Test]
        public void Variant_MirrorsOnlyItsLayer()
        {
            var (_, variant) = MakeVariant(_ => { });
            var contents = PrefabUtility.LoadPrefabContents(variant);

            try
            {
                var errors = new List<string>();
                var layer = HierarchyBindingsLayer.Resolve(variant, true);
                var node = HierarchyBindingsScanner.Scan(contents, "VariantBindings", layer, errors, false, false);

                Assert.That(errors, Is.Empty);
                Assert.That(node.Fields.Select(field => field.PropertyName), Is.EqualTo(new[] { "GameObject" }));

                var holder = node.Children.Single();
                Assert.That(holder.PropertyName, Is.EqualTo("Holder"));
                Assert.That(holder.Fields, Is.Empty);

                var added = holder.Children.Single();
                Assert.That(added.PropertyName, Is.EqualTo("Added"));
                Assert.That(added.Fields.Select(field => field.PropertyName),
                    Is.EqualTo(new[] { "GameObject", "Transform" }));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // An element of an array of references and of an array inside a nested class left empty: each is named.
        [Test]
        public void Validator_EmptyElementOfAnArray_IsAnError()
        {
            var rack = MakePrefab(TempRoot + "/Rack.prefab", root =>
            {
                var slots = new[] { Child(root, "Slot"), Child(root, "Slot (1)"), Child(root, "Slot (2)") };
                var serialized = new SerializedObject(root.AddComponent<RackBindings>());

                foreach (var name in new[] { "_slots", "_shelf._items" })
                {
                    var array = serialized.FindProperty(name);
                    array.arraySize = 3;
                    array.GetArrayElementAtIndex(0).objectReferenceValue = slots[0].transform;
                    array.GetArrayElementAtIndex(2).objectReferenceValue = slots[2].transform;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            var errors = CollectErrors(() => HierarchyBindingsValidator.ValidatePrefab(rack));

            Assert.That(errors, Has.Some.Contains("'_slots.Array.data[1]' is null"));
            Assert.That(errors, Has.Some.Contains("'_shelf._items.Array.data[1]' is null"));
            Assert.That(errors, Has.None.Contains("data[0]"));
            Assert.That(errors, Has.None.Contains("data[2]"));
        }

        // The game case: a child was replaced, the code and the structure hash stayed, the reference became empty.
        // Regenerate All binds such an object again instead of passing it as unchanged.
        [Test]
        public void PrepareProject_EmptyReference_IsBoundAgain()
        {
            var shop = MakePrefab(TempRoot + "/Menu/Shop.prefab", root => root.AddComponent<ShopBindings>());

            HierarchyBindingsBatch.RegeneratePrefabs(new[] { shop });
            Assert.That(HierarchyBindingsGenerator.PumpPending(), Is.EqualTo("Idle."));
            Assert.That(HierarchyBindingsBatch.PrepareProject(out _), Is.EqualTo(1));
            Assert.That(HierarchyBindingsQueue.IsEmpty(), Is.True, "A bound prefab is not bound again.");

            var contents = PrefabUtility.LoadPrefabContents(shop);

            try
            {
                var serialized = new SerializedObject(contents.GetComponent<ShopBindings>());
                serialized.FindProperty("_transform").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, shop);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(shop).GetComponent<ShopBindings>().Transform, Is.Null);

            Assert.That(HierarchyBindingsBatch.PrepareProject(out var changed), Is.EqualTo(1));
            Assert.That(changed, Is.False);
            Assert.That(HierarchyBindingsQueue.Load().Single().AssetPath, Is.EqualTo(shop));

            HierarchyBindingsGenerator.PumpPending();

            var bindings = AssetDatabase.LoadAssetAtPath<GameObject>(shop).GetComponent<ShopBindings>();
            Assert.That(bindings.Transform, Is.EqualTo(bindings.transform));
        }

        private static List<string> CollectErrors(Action action)
        {
            var errors = new List<string>();

            void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error)
                    errors.Add(condition);
            }

            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;

            try
            {
                action();
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                LogAssert.ignoreFailingMessages = false;
            }

            return errors;
        }

        // Base: root with the Holder/Inherited and Untouched branches and ShopBindings; variant: Holder/Added.
        private static (string basePath, string variantPath) MakeVariant(Action<GameObject> onAdded)
        {
            var basePath = MakePrefab(TempRoot + "/Base.prefab", root =>
            {
                root.AddComponent<ShopBindings>();
                Child(Child(root, "Holder"), "Inherited");
                Child(root, "Untouched");
            });
            var variantPath = TempRoot + "/Variant.prefab";
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath));

            try
            {
                onAdded(Child(instance.transform.Find("Holder").gameObject, "Added"));
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath, out var success);
                Assert.That(success, Is.True, variantPath);
                return (basePath, variantPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static HierarchyBindingsPrefabItem Item(string assetPath, string namespaceName)
        {
            return new HierarchyBindingsPrefabItem(
                0,
                0,
                namespaceName + ".ShopBindings",
                new HierarchyBindingsRequest
                {
                    AssetPath = assetPath,
                    ObjectPath = string.Empty,
                    TypeName = "ShopBindings",
                    Namespace = namespaceName,
                    IsPrefabAsset = true
                });
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
