using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal.EditorTests.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // Catalogue groups over the ContentRoot of ContentBundleTestBuild, bound by CatalogContent for the lifetime of a
    // root (which holds the main bundle too): Retain loads the group asset and Release releases it, a preloaded group is
    // fetched by the binding and loads on its first access without a Retain, the end of the root unloads every group and
    // keeps the retains to release.
    [PrebuildSetup("Internal.EditorTests.ContentBundleTestBuild")]
    [PostBuildCleanup("Internal.EditorTests.ContentBundleTestBuild")]
    public class AssetGroupTests
    {
        private const int ReleaseFrames = 120;

        private Lifetime _lifetime;
        private ILifetime _root;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
            ContentTestSets.DeleteFetched();
            _root = _lifetime.Child();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
            ContentTestSets.DeleteFetched();
            AssetBundle.UnloadAllAssetBundles(true);
        }

        [UnityTest]
        public IEnumerator Retain_Loads_Release_Released() => UniTask.ToCoroutine(async () =>
        {
            var root = await BindLocal();
            var group = new TestGroup(root.Groups.Single().Guid);
            var before = Groups();

            Assert.IsFalse(group.IsLoaded);
            Assert.Throws<InvalidOperationException>(group.EnsureLoaded, "A group that is not preloaded loads only through Retain");

            await group.Retain();

            var loaded = group.Loaded;
            Assert.IsTrue(group.IsLoaded);
            Assert.AreEqual("Group", loaded.name);
            Assert.AreEqual(before + 1, Groups(), "A copy of the bundle is loaded");
            Assert.DoesNotThrow(group.EnsureLoaded);

            group.Release();

            Assert.IsFalse(group.IsLoaded);
            Assert.IsNull(group.Loaded);
            Assert.IsTrue(loaded != null, "The main bundle is held by the root: an AssetBundle frees its objects only with itself");
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator TwoRetains_FirstReleased_Loaded_SecondReleased_Unloaded() => UniTask.ToCoroutine(async () =>
        {
            var root = await BindLocal();
            var group = new TestGroup(root.Groups.Single().Guid);

            await UniTask.WhenAll(group.Retain(), group.Retain());
            var loaded = group.Loaded;

            group.Release();
            await UniTask.DelayFrame(10);

            Assert.IsTrue(group.IsLoaded);
            Assert.IsTrue(loaded != null, "Alive while the second retain holds it");

            group.Release();

            Assert.IsFalse(group.IsLoaded);
            Assert.Throws<InvalidOperationException>(group.Release, "Release without a Retain");
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator Preloaded_FetchedByBinding_EnsureLoadedWithoutRetain() => UniTask.ToCoroutine(async () =>
        {
            var loader = await ContentEntryPoint.Over(ContentTestSets.Fetched(ContentTestSets.Store(false))).Register(_root, ContentTestPaths.Name);
            var root = loader.RootAssets<ContentRoot>().Single();
            var preloadedReference = root.PreloadedGroups.Single();

            await CatalogContent.Bind(_root, loader, new[] { root });
            var group = new TestPreloadedGroup(preloadedReference.Guid);

            group.EnsureLoaded();

            Assert.IsTrue(group.IsLoaded, "Loaded synchronously on the first access");
            Assert.AreEqual("Preloaded", group.Loaded.name);

            // A preloaded group stays loaded after its retains are released.
            await group.Retain();
            group.Release();
            Assert.IsTrue(group.IsLoaded);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator RootEnds_GroupsUnloaded_ReleaseAfterDoesNotThrow() => UniTask.ToCoroutine(async () =>
        {
            var root = await BindLocal();
            var group = new TestGroup(root.Groups.Single().Guid);
            var preloaded = new TestPreloadedGroup(root.PreloadedGroups.Single().Guid);
            var before = Groups();

            await group.Retain();
            preloaded.EnsureLoaded();
            Assert.AreEqual(before + 2, Groups());

            _root.Terminate();

            Assert.IsFalse(group.IsLoaded);
            Assert.IsFalse(preloaded.IsLoaded);
            Assert.IsNull(group.Loaded);
            Assert.IsNull(preloaded.Loaded);
            await WaitGroups(before);

            Assert.DoesNotThrow(group.Release, "The retain stays counted after the end of the root");
            Assert.Throws<InvalidOperationException>(preloaded.EnsureLoaded, "Unbound: no content to load from");
            LogAssert.NoUnexpectedReceived();
        });

        private async UniTask<ContentRoot> BindLocal()
        {
            var loader = await ContentEntryPoint.Over(ContentTestSets.Local()).Register(_root, ContentTestPaths.Name);
            var root = loader.RootAssets<ContentRoot>().Single();
            await CatalogContent.Bind(_root, loader, new[] { root });
            return root;
        }

        private static int Groups()
        {
            return Resources.FindObjectsOfTypeAll<ContentTestGroup>().Length;
        }

        // The engine releases an object asynchronously.
        private static async UniTask WaitGroups(int count)
        {
            for (var frame = 0; frame < ReleaseFrames && Groups() != count; frame++)
                await UniTask.Yield();

            Assert.AreEqual(count, Groups(), "The loaded copies are destroyed after the release");
        }
    }
}
