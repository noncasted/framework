using System;
using System.Collections;
using System.IO;
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
    // IContentLoader over the content directory of ContentDirectoryTestBuild: registered over a local store (the files on
    // the disk), over a fetched store (zeros until a load fetches them), and the assets of the project without a
    // registered directory (EditorContentDirectory, Play Mode of the editor). The references are the ones of the root
    // ContentTestReferences, written by the editor.
    [PrebuildSetup("Internal.EditorTests.ContentDirectoryTestBuild")]
    [PostBuildCleanup("Internal.EditorTests.ContentDirectoryTestBuild")]
    public class ContentLoaderTests
    {
        private const string FetchedFolder = ContentTestPaths.Output + "/LoaderFetched";
        private const int ReleaseFrames = 120;

        private ContentIndex _index;
        private Lifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _index = ContentIndex.FromJson(File.ReadAllText(ContentTestPaths.Index));
            _lifetime = new Lifetime();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();

            if (Directory.Exists(FetchedFolder))
                Directory.Delete(FetchedFolder, true);
        }

        [UnityTest]
        public IEnumerator Load_LifetimeEnds_Released() => UniTask.ToCoroutine(async () =>
        {
            var (loader, references) = await RegisterLocal();
            var before = Targets();
            var lifetime = new Lifetime();

            Assert.Throws<InvalidOperationException>(() => _ = references.Target.Target, "Target before the load");
            var target = await loader.Load(lifetime, references.Target);

            Assert.AreEqual("Target", target.name);
            Assert.IsTrue(references.Target.IsLoaded);
            Assert.AreSame(target, references.Target.Target);
            Assert.AreEqual(before + 1, Targets(), "A copy of the directory is loaded");

            lifetime.Terminate();

            Assert.IsFalse(references.Target.IsLoaded);
            await WaitTargets(before);
            Assert.IsTrue(target == null, "The object is destroyed after its release");
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator LifetimeEndsDuringLoad_Canceled_Released() => UniTask.ToCoroutine(async () =>
        {
            var (loader, references) = await RegisterLocal();
            var before = Targets();
            var lifetime = new Lifetime();

            var load = loader.Load(lifetime, references.Target);
            lifetime.Terminate();

            await AssertCanceled(load);
            await WaitTargets(before);
            Assert.IsFalse(references.Target.IsLoaded);

            // The next load of the reference starts over.
            var target = await loader.Load(_lifetime, references.Target);
            Assert.AreEqual("Target", target.name);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator LifetimeEndsDuringFetch_Canceled_NothingLoaded() => UniTask.ToCoroutine(async () =>
        {
            var (loader, references) = await RegisterFetched();
            var lifetime = new Lifetime();

            var load = loader.Load(lifetime, references.Target);
            lifetime.Terminate();

            await AssertCanceled(load);
            Assert.IsFalse(references.Target.IsLoaded);
            Assert.Throws<InvalidOperationException>(() => loader.LoadNow(_lifetime, references.Target), "Still zeros: a fetch is needed");
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator TwoLoads_FirstEnds_Alive_SecondEnds_Released() => UniTask.ToCoroutine(async () =>
        {
            var (loader, references) = await RegisterLocal();
            var before = Targets();
            var first = new Lifetime();
            var second = new Lifetime();

            var loads = await UniTask.WhenAll(loader.Load(first, references.Target), loader.Load(second, references.Target));
            Assert.AreSame(loads.Item1, loads.Item2);

            first.Terminate();
            await UniTask.DelayFrame(10);

            Assert.IsTrue(references.Target.IsLoaded);
            Assert.IsTrue(loads.Item1 != null, "Alive while the second lifetime holds it");

            second.Terminate();

            Assert.IsFalse(references.Target.IsLoaded);
            await WaitTargets(before);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator LoadList_LoadsEach() => UniTask.ToCoroutine(async () =>
        {
            var (loader, references) = await RegisterLocal();

            var targets = await loader.Load(_lifetime, new[] { references.Target, references.Target });

            Assert.AreEqual(2, targets.Count);
            Assert.AreSame(targets[0], targets[1]);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator Scene_LoadsAndUnloadsByLifetime() => UniTask.ToCoroutine(async () =>
        {
            var (loader, references) = await RegisterLocal();
            await AssertSceneByLifetime(loader, references.Scene);
        });

        [UnityTest]
        public IEnumerator FetchedStore_LoadFetches_Progress_LoadNowAfter() => UniTask.ToCoroutine(async () =>
        {
            var (loader, references) = await RegisterFetched();
            var progress = loader.Progress(references.Target);
            var total = _index.ObjectFiles(references.Target.Guid, references.Target.LocalId).Sum(file => file.Size);

            Assert.AreEqual(total, progress.Value.Total);
            Assert.Throws<InvalidOperationException>(() => loader.LoadNow(_lifetime, references.Target), "Zeros: LoadNow does not wait");

            var lifetime = new Lifetime();
            var target = await loader.Load(lifetime, references.Target);

            Assert.AreEqual("Target", target.name);
            Assert.AreEqual(total, progress.Value.Loaded);
            Assert.IsTrue(progress.Value.IsDone);

            lifetime.Terminate();
            var now = loader.LoadNow(_lifetime, references.Target);
            Assert.AreEqual("Target", now.name, "Fetched already: loaded without a frame");

            await AssertSceneByLifetime(loader, references.Scene);
        });

        [UnityTest]
        public IEnumerator EditorKeys_MatchTheIndex() => UniTask.ToCoroutine(async () =>
        {
            var (_, references) = await RegisterLocal();
            var target = references.Target;

            Assert.AreEqual(File.ReadAllText(ContentTestPaths.TargetGuid), target.Guid);
            Assert.IsNotEmpty(_index.ObjectFiles(target.Guid, target.LocalId));
            Assert.AreNotEqual(0, target.LocalId);

            // The engine does not promise this format: a check of the keys only.
            StringAssert.Contains($"guid: {target.Guid}", target.Id.ToString());
            StringAssert.Contains($"fileID: {target.LocalId}", target.Id.ToString());

            Assert.IsTrue(_index.Scenes.Any(scene => scene.Guid == references.Scene.Guid));
            Assert.AreEqual(references.Scene.Guid, references.Scene.Id.ToString());
        });

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator EditorDirectory_LoadsTheProjectAssets() => UniTask.ToCoroutine(async () =>
        {
            var references = AssetDatabase.LoadAssetAtPath<ContentTestReferences>(ContentTestPaths.ReferencesAsset);
            var loader = new ContentLoader(new EditorContentDirectory(ContentTestPaths.Name, new Object[] { references }));
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

            await AssertSceneByLifetime(loader, references.Scene);
        });
#endif

        private async UniTask<(ContentLoader, ContentTestReferences)> RegisterLocal()
        {
            var directory = await ContentDirectory.Register(_lifetime, _index, new LocalContentStore(Path.GetFullPath(ContentTestPaths.Folder)));
            return (new ContentLoader(directory), directory.RootAssets<ContentTestReferences>().Single());
        }

        private async UniTask<(ContentLoader, ContentTestReferences)> RegisterFetched()
        {
            var store = new FetchedContentStore(new CopyContentFileSource(ContentTestPaths.Folder), Path.GetFullPath(FetchedFolder));
            var directory = await ContentDirectory.Register(_lifetime, _index, store);
            return (new ContentLoader(directory), directory.RootAssets<ContentTestReferences>().Single());
        }

        private static async UniTask AssertSceneByLifetime(IContentLoader loader, ContentScene scene)
        {
            var lifetime = new Lifetime();
            var count = SceneManager.sceneCount;

            var loaded = await loader.LoadScene(lifetime, scene, true);

            Assert.IsTrue(loaded.Scene.isLoaded);
            Assert.AreEqual("Level", loaded.Scene.name);
            Assert.AreEqual(loaded.Scene, SceneManager.GetActiveScene(), "isMain makes it active");
            Assert.AreEqual(count + 1, SceneManager.sceneCount);

            lifetime.Terminate();

            for (var frame = 0; frame < ReleaseFrames && SceneManager.sceneCount > count; frame++)
                await UniTask.Yield();

            Assert.AreEqual(count, SceneManager.sceneCount, "Unloaded when its lifetime ends");
            Assert.IsFalse(loaded.Scene.isLoaded);
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
