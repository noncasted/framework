using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal.EditorTests.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // Catalogue groups over the ContentRoot of ContentDirectoryTestBuild, bound by CatalogContent for the lifetime of a
    // root: Retain loads the group asset and Release releases it, a preloaded group is fetched by the binding and loads
    // on its first access without a Retain, the end of the root unloads every group and keeps the retains to release.
    [PrebuildSetup("Internal.EditorTests.ContentDirectoryTestBuild")]
    [PostBuildCleanup("Internal.EditorTests.ContentDirectoryTestBuild")]
    public class AssetGroupTests
    {
        private const string FetchedFolder = ContentTestPaths.Output + "/GroupsFetched";
        private const int ReleaseFrames = 120;

        private ContentIndex _index;
        private Lifetime _lifetime;
        private ILifetime _root;

        [SetUp]
        public void SetUp()
        {
            _index = ContentIndex.FromJson(File.ReadAllText(ContentTestPaths.Index));
            _lifetime = new Lifetime();
            _root = _lifetime.Child();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();

            if (Directory.Exists(FetchedFolder))
                Directory.Delete(FetchedFolder, true);
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
            Assert.AreEqual(before + 1, Groups(), "A copy of the directory is loaded");
            Assert.DoesNotThrow(group.EnsureLoaded);

            group.Release();

            Assert.IsFalse(group.IsLoaded);
            Assert.IsNull(group.Loaded);
            await WaitGroups(before);
            Assert.IsTrue(loaded == null, "The asset is destroyed after the release");
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
            var store = new FetchedContentStore(new CopyContentRangeSource(ContentTestPaths.Archive), Path.GetFullPath(FetchedFolder), ContentOptions.DefaultRangeGap);
            var directory = await ContentDirectory.Register(_lifetime, _index, store);
            var loader = new ContentLoader(directory);
            var root = directory.RootAssets<ContentRoot>().Single();

            var preloadedReference = root.PreloadedGroups.Single();
            Assert.Throws<InvalidOperationException>(() => loader.LoadNow(_lifetime, preloadedReference), "Zeros before the binding fetches it");

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
            var directory = await ContentDirectory.Register(_lifetime, _index, new LocalContentStore(Path.GetFullPath(ContentTestPaths.Folder)));
            var root = directory.RootAssets<ContentRoot>().Single();
            await CatalogContent.Bind(_root, new ContentLoader(directory), new[] { root });
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
