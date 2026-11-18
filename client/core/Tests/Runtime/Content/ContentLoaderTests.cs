using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal.EditorTests.Content;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Internal.Tests
{
    // IContentLoader over the bundles of ContentBundleTestBuild: read in place (the files on the disk), fetched (a file
    // real once fetched, LoadNow before it throws) and the assets of the project (EditorContentSource, Play Mode of the
    // editor). The main loader registers Tests (held while the test lives); the target of TestsExtra loads its bundle and
    // TestsShared, which its target references directly, and the end of the last lifetime unloads both. No bundle is left
    // loaded after a test.
    [PrebuildSetup("Internal.EditorTests.ContentBundleTestBuild")]
    [PostBuildCleanup("Internal.EditorTests.ContentBundleTestBuild")]
    public class ContentLoaderTests
    {
        private const int ReleaseFrames = 120;

        private Lifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
            ContentTestSets.DeleteFetched();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
            ContentTestSets.DeleteFetched();

            var left = AssetBundle.GetAllLoadedAssetBundles().Select(bundle => bundle.name).ToArray();
            AssetBundle.UnloadAllAssetBundles(true);
            Assert.IsEmpty(left, "Every bundle is unloaded with the lifetimes that held it");
        }

        [UnityTest]
        public IEnumerator Load_LoadsTheBundleWithItsDependency_LifetimeEnds_BothUnloaded() => UniTask.ToCoroutine(async () =>
        {
            var (loader, extra) = await Register(ContentTestSets.Local());
            var before = Targets();
            var lifetime = new Lifetime();

            Assert.Throws<InvalidOperationException>(() => _ = extra.Target, "Target before the load");
            var target = await loader.Load(lifetime, extra);

            Assert.AreEqual("ExtraTarget", target.name);
            Assert.IsTrue(extra.IsLoaded);
            Assert.AreSame(target, extra.Target);
            Assert.AreEqual("Shared", target.Direct.name, "The direct reference into TestsShared");
            Assert.AreEqual(before + 1, Targets(), "A copy of the bundle is loaded");
            var shared = target.Direct;

            lifetime.Terminate();

            Assert.IsFalse(extra.IsLoaded);
            await WaitTargets(before);
            Assert.IsTrue(target == null, "Destroyed with its bundle");
            Assert.IsTrue(shared == null, "The dependency is unloaded too");
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator LifetimeEndsDuringLoad_Canceled_Released() => UniTask.ToCoroutine(async () =>
        {
            var (loader, extra) = await Register(ContentTestSets.Local());
            var before = Targets();
            var lifetime = new Lifetime();

            var load = loader.Load(lifetime, extra);
            lifetime.Terminate();

            await AssertCanceled(load);
            await UniTask.DelayFrame(10);
            await WaitTargets(before);
            Assert.IsFalse(extra.IsLoaded);

            // The next load starts over, after the abandoned one is unloaded.
            var target = await loader.Load(_lifetime, extra);
            Assert.AreEqual("ExtraTarget", target.name);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator LifetimeEndsDuringFetch_Canceled_FetchAborted_NothingLoaded() => UniTask.ToCoroutine(async () =>
        {
            var store = ContentTestSets.Store(false);
            var (loader, extra) = await Register(ContentTestSets.Fetched(store));
            store.Shut();
            var lifetime = new Lifetime();

            var load = loader.Load(lifetime, extra);
            await UniTask.DelayFrame(2);
            lifetime.Terminate();

            await AssertCanceled(load);
            await UniTask.DelayFrame(2);
            Assert.IsFalse(extra.IsLoaded);
            Assert.Greater(store.Aborts, 0, "Nobody waits for the files: the fetch is aborted");
            Assert.Throws<InvalidOperationException>(() => loader.LoadNow(_lifetime, extra), "Not fetched: LoadNow does not wait");
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator TwoLoads_FirstEnds_Alive_SecondEnds_Released() => UniTask.ToCoroutine(async () =>
        {
            var (loader, extra) = await Register(ContentTestSets.Local());
            var before = Targets();
            var first = new Lifetime();
            var second = new Lifetime();

            var loads = await UniTask.WhenAll(loader.Load(first, extra), loader.Load(second, extra));
            Assert.AreSame(loads.Item1, loads.Item2);

            first.Terminate();
            await UniTask.DelayFrame(10);

            Assert.IsTrue(extra.IsLoaded);
            Assert.IsTrue(loads.Item1 != null, "Alive while the second lifetime holds it");

            second.Terminate();

            Assert.IsFalse(extra.IsLoaded);
            await WaitTargets(before);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator TargetOfTheRegisteredBundle_StaysWhileTheBundleIsHeld() => UniTask.ToCoroutine(async () =>
        {
            var (loader, _) = await Register(ContentTestSets.Local());
            var references = loader.RootAssets<ContentTestReferences>().Single();
            var lifetime = new Lifetime();

            var targets = await loader.Load(lifetime, new[] { references.Target, references.Target });
            Assert.AreEqual(2, targets.Count);
            Assert.AreSame(targets[0], targets[1]);
            Assert.AreEqual(0, loader.Progress(references.Target).Value.Total, "Read in place: nothing to fetch");

            lifetime.Terminate();

            Assert.IsFalse(references.Target.IsLoaded, "Released by the reference");
            Assert.IsTrue(targets[0] != null, "The bundle is held by the registration: an AssetBundle frees its objects only with itself");
            Assert.AreSame(targets[0], loader.LoadNow(_lifetime, references.Target), "Loaded synchronously from the held bundle");
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator Scene_LoadsAndUnloadsByLifetime_OfTheMainAndOfAnotherBundle() => UniTask.ToCoroutine(async () =>
        {
            var (loader, _) = await Register(ContentTestSets.Local());
            var references = loader.RootAssets<ContentTestReferences>().Single();
            var extraScene = ContentTestSets.Catalog.Scenes.Single(scene => scene.Bundle == ContentTestPaths.ExtraName);

            await AssertSceneByLifetime(loader, references.Scene, "Level");
            await AssertSceneByLifetime(loader, new ContentScene(extraScene.Guid), "ExtraLevel");
        });

        [UnityTest]
        public IEnumerator Fetched_LoadFetches_Progress_LoadNowWhileHeld_ForgottenAfter() => UniTask.ToCoroutine(async () =>
        {
            var store = ContentTestSets.Store(false);
            var (loader, extra) = await Register(ContentTestSets.Fetched(store));
            var progress = loader.Progress(extra);
            var total = ExtraFiles().Sum(file => file.Size);

            Assert.AreEqual(total, progress.Value.Total, "The bundle and its dependency");
            Assert.AreEqual(0, progress.Value.Loaded);
            Assert.Throws<InvalidOperationException>(() => loader.LoadNow(_lifetime, extra), "Not fetched: LoadNow does not wait");

            var lifetime = new Lifetime();
            var target = await loader.Load(lifetime, extra);

            Assert.AreEqual("ExtraTarget", target.name);
            Assert.AreEqual(total, progress.Value.Loaded);
            Assert.IsTrue(progress.Value.IsDone);

            var held = new Lifetime();
            Assert.AreSame(target, loader.LoadNow(held, extra), "Loaded: LoadNow gives it at once");
            held.Terminate();

            var forgotten = store.Forgotten;
            lifetime.Terminate();

            Assert.AreEqual(forgotten + ExtraFiles().Length, store.Forgotten, "The fetched files go with the unload of their bundles");
            Assert.Throws<InvalidOperationException>(() => loader.LoadNow(_lifetime, extra), "Forgotten: a fetch again");
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator Prefetch_FetchesOnce_LoadsNothing_LoadNowAfter() => UniTask.ToCoroutine(async () =>
        {
            var store = ContentTestSets.Store(false);
            var (loader, extra) = await Register(ContentTestSets.Fetched(store));
            var fetches = store.Fetches;
            var before = Targets();

            await UniTask.WhenAll(loader.Fetch(_lifetime, extra), loader.Fetch(_lifetime, extra));

            Assert.AreEqual(fetches + ExtraFiles().Length, store.Fetches, "One fetch per file for both");
            Assert.AreEqual(before, Targets(), "Nothing loaded");
            Assert.IsTrue(loader.Progress(extra).Value.IsDone);

            var target = loader.LoadNow(_lifetime, extra);
            Assert.AreEqual("ExtraTarget", target.name, "Fetched: loaded without a frame");
            Assert.AreEqual("Shared", target.Direct.name);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator TwoLoadsDuringFetch_FirstEnds_FetchGoesOn_OneFetchPerFile() => UniTask.ToCoroutine(async () =>
        {
            var store = ContentTestSets.Store(false);
            var (loader, extra) = await Register(ContentTestSets.Fetched(store));
            store.Shut();
            var fetches = store.Fetches;
            var aborts = store.Aborts;
            var first = new Lifetime();
            var second = new Lifetime();

            var prefetch = loader.Fetch(first, extra);
            var load = loader.Load(second, extra);
            await UniTask.DelayFrame(2);
            first.Terminate();

            await AssertCanceled(prefetch);
            await UniTask.DelayFrame(2);
            store.Open();

            var target = await load;

            Assert.AreEqual("ExtraTarget", target.name, "The load that still waits gets its target");
            Assert.AreEqual("Shared", target.Direct.name);
            Assert.AreEqual(aborts, store.Aborts, "A waiter is left: the fetch goes on");
            Assert.AreEqual(fetches + ExtraFiles().Length, store.Fetches, "One fetch per file for both");

            second.Terminate();
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator Registration_OfAnUnknownBundle_Throws() => UniTask.ToCoroutine(async () =>
        {
            var runtime = ContentRuntime.Over(ContentTestSets.Local());

            try
            {
                await runtime.Register(_lifetime, "Missing");
                Assert.Fail("An unknown bundle registers");
            }
            catch (System.Collections.Generic.KeyNotFoundException exception)
            {
                StringAssert.Contains("No content bundle Missing", exception.Message);
            }
        });

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator EditorSource_LoadsTheProjectAssets() => UniTask.ToCoroutine(async () =>
        {
            var references = AssetDatabase.LoadAssetAtPath<ContentTestReferences>(ContentTestPaths.ReferencesAsset);
            var loader = new ContentLoader(new EditorContentSource(ContentTestPaths.Name, new Object[] { references }), new ContentLoads());
            var asset = AssetDatabase.LoadAssetAtPath<ContentTestTarget>(ContentTestPaths.TargetAsset);
            var lifetime = new Lifetime();

            Assert.AreSame(references, loader.RootAssets<ContentTestReferences>().Single());
            Assert.AreSame(asset, references.Target.EditorAsset);
            Assert.AreEqual(ContentTestPaths.SceneAsset, references.Scene.EditorPath);

            var target = await loader.Load(lifetime, references.Target);
            Assert.AreSame(asset, target, "The asset of the project");
            Assert.AreEqual(0, loader.Progress(references.Target).Value.Total);

            lifetime.Terminate();
            await UniTask.DelayFrame(10);

            Assert.IsFalse(references.Target.IsLoaded);
            Assert.IsTrue(asset != null, "A release keeps the asset of the project");
            Assert.AreSame(asset, loader.LoadNow(_lifetime, references.Target));

            await AssertSceneByLifetime(loader, references.Scene, "Level");
        });
#endif

        // The main loader over Tests, and the reference of the catalog to the target of TestsExtra.
        private async UniTask<(IContentLoader, ContentReference<ContentTestTarget>)> Register(ContentBundleSet set)
        {
            var loader = await ContentRuntime.Over(set).Register(_lifetime, ContentTestPaths.Name);
            var entry = set.Catalog.Objects.Single(target => target.Bundle == ContentTestPaths.ExtraName);
            return (loader, new ContentReference<ContentTestTarget>(entry.Guid, entry.LocalId));
        }

        // The files of TestsExtra and of TestsShared, its dependency.
        private static ContentCatalogFileEntry[] ExtraFiles()
        {
            var catalog = ContentTestSets.Catalog;
            return new[] { ContentTestPaths.ExtraName, ContentTestPaths.SharedName }.SelectMany(name => catalog.Bundle(name).Files).ToArray();
        }

        private static async UniTask AssertSceneByLifetime(IContentLoader loader, ContentScene scene, string name)
        {
            var lifetime = new Lifetime();
            var count = SceneManager.sceneCount;

            var loaded = await loader.LoadScene(lifetime, scene, true);

            Assert.IsTrue(loaded.Scene.isLoaded);
            Assert.AreEqual(name, loaded.Scene.name);
            Assert.AreEqual(loaded.Scene, SceneManager.GetActiveScene(), "isMain makes it active");
            Assert.AreEqual(count + 1, SceneManager.sceneCount);

            lifetime.Terminate();

            for (var frame = 0; frame < ReleaseFrames && SceneManager.sceneCount > count; frame++)
                await UniTask.Yield();

            Assert.AreEqual(count, SceneManager.sceneCount, "Unloaded when its lifetime ends");
            Assert.IsFalse(loaded.Scene.isLoaded);
            await UniTask.DelayFrame(2);
            LogAssert.NoUnexpectedReceived();
        }

        private static async UniTask AssertCanceled(UniTask<ContentTestTarget> load)
        {
            try
            {
                await load;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Assert.Fail("The load of an ended lifetime gives OperationCanceledException");
        }

        private static async UniTask AssertCanceled(UniTask fetch)
        {
            try
            {
                await fetch;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Assert.Fail("The fetch of an ended lifetime gives OperationCanceledException");
        }

        private static int Targets()
        {
            return Resources.FindObjectsOfTypeAll<ContentTestTarget>().Length;
        }

        // The engine releases an object asynchronously.
        private static async UniTask WaitTargets(int count)
        {
            for (var frame = 0; frame < ReleaseFrames && Targets() != count; frame++)
                await UniTask.Yield();

            Assert.AreEqual(count, Targets(), "The loaded copy is destroyed after its release");
        }
    }
}
