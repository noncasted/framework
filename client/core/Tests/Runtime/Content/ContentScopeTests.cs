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
    // Scopes over the bundles of ContentBundleTestBuild: the scene of a scope (WithAssetScene) and its
    // content (LoadContent, RequestContent) are loaded on the lifetime of the scope and go with its dispose; a child takes
    // the loader of its root, the one the root registers or AddContent creates.
    [PrebuildSetup("Internal.EditorTests.ContentBundleTestBuild")]
    [PostBuildCleanup("Internal.EditorTests.ContentBundleTestBuild")]
    public class ContentScopeTests
    {
        private const int ReleaseFrames = 120;

        private Lifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _lifetime = new Lifetime();
            ContentScopeRoots.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            _lifetime.Terminate();
            ContentScopeRoots.Reset();
            AssetBundle.UnloadAllAssetBundles(true);
        }

        [UnityTest]
        public IEnumerator RegisteredBundle_ScopeLoadsSceneAndContent_DisposeReleases() => UniTask.ToCoroutine(async () =>
        {
            var loader = await ContentRuntime.Over(ContentTestSets.Local()).Register(_lifetime, ContentTestPaths.Name);
            var references = loader.RootAssets<ContentTestReferences>().Single();
            var root = await new ServiceScopeLoader().Load(ScopeLoadOptions.Create(null, ContentScopeRoots.Root.Construct, loader));

            await AssertChild(root, references);

            await root.Dispose();
            LogAssert.NoUnexpectedReceived();
        });

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AddContent_EditorSource_ChildLoadsTheProjectAssets() => UniTask.ToCoroutine(async () =>
        {
            var references = AssetDatabase.LoadAssetAtPath<ContentTestReferences>(ContentTestPaths.ReferencesAsset);
            var previous = EditorContentSources.Install(name => new EditorContentSource(name, new Object[] { references }));

            try
            {
                var options = JsonUtility.FromJson<ContentOptions>($"{{\"_name\":\"{ContentTestPaths.Name}\"}}");
                var root = await new ServiceScopeLoader().Load(ScopeLoadOptions.Create(null, ContentScopeRoots.AddedRoot.Construct, options));

                Assert.AreSame(references, ((ContentLoader)root.Resolve<IContentLoader>()).RootAssets<ContentTestReferences>().Single());

                await AssertChild(root, references);
                Assert.AreSame(references.Target.EditorAsset, ContentScopeRoots.Loaded, "The asset of the project");

                await root.Dispose();
            }
            finally
            {
                EditorContentSources.Install(previous);
            }

            LogAssert.NoUnexpectedReceived();
        });
#endif

#if UNITY_EDITOR
        // The root registers a second bundle next to the one of the catalogues: each has its own loader, an object of the
        // second loads from a reference of its roots and goes with the root.
        [UnityTest]
        public IEnumerator AddContentBundle_SecondBundle_LoadsFromItsRoots() => UniTask.ToCoroutine(async () =>
        {
            var (main, extra) = await RegisterBoth();
            var previous = EditorContentSources.Install(name => name == ContentTestPaths.Name ? main : name == ContentTestPaths.ExtraName ? extra :
                throw new InvalidOperationException($"No test bundle {name}"));

            try
            {
                var options = new[] { new ContentOptions(ContentTestPaths.Name), new ContentOptions(ContentTestPaths.ExtraName) };
                var root = await new ServiceScopeLoader().Load(ScopeLoadOptions.Create(null, ContentScopeRoots.TwoBundles.Construct, options));
                var bundles = root.Resolve<IContentBundles>();
                var loader = await bundles.Wait(root.Lifetime, ContentTestPaths.ExtraName);
                var references = loader.RootAssets<ContentTestReferences>().Single();

                Assert.AreSame(root.Resolve<IContentLoader>(), bundles.Get(ContentTestPaths.Name), "The loader of AddContent");
                Assert.AreNotSame(root.Resolve<IContentLoader>(), loader);
                Assert.AreSame(loader, bundles.Get(ContentTestPaths.ExtraName), "Get once registered");
                Assert.IsEmpty(root.Resolve<IContentLoader>().RootAssets<ContentTestReferences>().Where(asset => asset.name == "ExtraReferences"));

                var target = await loader.Load(root.Lifetime, references.Target);

                Assert.AreEqual("ExtraTarget", target.name);
                Assert.AreEqual(loader.Progress(references.Target).Value.Total, loader.Progress(references.Target).Value.Loaded, "Its files are real");
                Assert.AreEqual("Shared", target.Direct.name, "With its dependency");
                Assert.Throws<InvalidOperationException>(() => bundles.Get("Missing"));
                Assert.Throws<InvalidOperationException>(() => bundles.Wait(root.Lifetime, "Missing"));

                var scene = await loader.LoadScene(root.Lifetime, references.Scene);
                Assert.IsTrue(scene.Scene.isLoaded, "A scene of the second bundle");

                await root.Dispose();
                Assert.IsFalse(references.Target.IsLoaded, "Released with the root");
            }
            finally
            {
                EditorContentSources.Install(previous);
            }

            await UniTask.DelayFrame(ReleaseFrames);
            LogAssert.NoUnexpectedReceived();
        });

        // The sources of the two bundles over one set, registered for the test.
        private async UniTask<(IContentSource, IContentSource)> RegisterBoth()
        {
            var set = ContentTestSets.Local();
            return (await BundleContentSource.Register(_lifetime, set, ContentTestPaths.Name), await BundleContentSource.Register(_lifetime, set, ContentTestPaths.ExtraName));
        }

        // OnFirstWait: the bundle is not registered (nothing fetched) until its first Wait, then it is as a background one.
        [UnityTest]
        public IEnumerator AddContentBundle_OnFirstWait_RegistersAtTheFirstWait() => UniTask.ToCoroutine(async () =>
        {
            var (main, extra) = await RegisterBoth();
            var created = 0;
            var previous = EditorContentSources.Install(name =>
            {
                if (name == ContentTestPaths.ExtraName)
                {
                    created++;
                    return extra;
                }

                return name == ContentTestPaths.Name ? main : throw new InvalidOperationException($"No test bundle {name}");
            });

            try
            {
                var options = new[] { new ContentOptions(ContentTestPaths.Name), new ContentOptions(ContentTestPaths.ExtraName) };
                var root = await new ServiceScopeLoader().Load(ScopeLoadOptions.Create(null, ContentScopeRoots.Deferred.Construct, options));
                var bundles = root.Resolve<IContentBundles>();
                await UniTask.DelayFrame(2);

                Assert.AreEqual(0, created, "Nothing before the first Wait");
                Assert.Throws<InvalidOperationException>(() => bundles.Get(ContentTestPaths.ExtraName));

                var loader = await bundles.Wait(root.Lifetime, ContentTestPaths.ExtraName);
                var again = await bundles.Wait(root.Lifetime, ContentTestPaths.ExtraName);
                var target = await loader.Load(root.Lifetime, loader.RootAssets<ContentTestReferences>().Single().Target);

                Assert.AreEqual(1, created, "One registration");
                Assert.AreSame(loader, again);
                Assert.AreEqual("ExtraTarget", target.name);

                await root.Dispose();
            }
            finally
            {
                EditorContentSources.Install(previous);
            }

            await UniTask.DelayFrame(ReleaseFrames);
            LogAssert.NoUnexpectedReceived();
        });

#endif

        [UnityTest]
        public IEnumerator AddContentBundle_WithoutAddContent_Throws() => UniTask.ToCoroutine(async () =>
        {
            var caught = default(Exception);

            try
            {
                await new ServiceScopeLoader().Load(ScopeLoadOptions.Create(null, ContentScopeRoots.ExtraOnly.Construct,
                    new ContentOptions(ContentTestPaths.ExtraName)));
            }
            catch (Exception exception)
            {
                caught = exception;
            }

            Assert.IsInstanceOf<InvalidOperationException>(caught);
            StringAssert.Contains("AddContentBundle needs AddContent", caught.Message);
        });

        [UnityTest]
        public IEnumerator RootWithoutContent_ContentScene_Throws() => UniTask.ToCoroutine(async () =>
        {
            var options = new ScopeLoadOptions(null, ContentScopeRoots.Empty.Construct).WithAssetScene(new ContentScene("none"));
            var caught = default(Exception);

            try
            {
                await new ServiceScopeLoader().Load(options);
            }
            catch (Exception exception)
            {
                caught = exception;
            }

            Assert.IsInstanceOf<InvalidOperationException>(caught, "A root without AddContent loads no content scene");
            StringAssert.Contains("AddContent", caught.Message);
        });

        private static async UniTask AssertChild(ILoadedScope root, ContentTestReferences references)
        {
            var scenes = SceneManager.sceneCount;
            var options = ScopeLoadOptions.Create(root, ContentScopeRoots.Child.Construct, references).WithAssetScene(references.Scene);
            var child = await new ServiceScopeLoader().Load(options);

            Assert.AreEqual(scenes + 1, SceneManager.sceneCount, "The scene of the scope");
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            Assert.IsTrue(scene.isLoaded);
            Assert.IsTrue(references.Target.IsLoaded, "LoadContent");
            Assert.AreSame(ContentScopeRoots.Loaded, ContentScopeRoots.Requested.Value, "RequestContent after the build");

            await child.Dispose();

            Assert.IsFalse(scene.isLoaded, "The dispose of the scope waits for the unload of its scene");
            Assert.AreEqual(scenes, SceneManager.sceneCount);
            Assert.IsFalse(references.Target.IsLoaded, "The content of the scope is released with it");

            await UniTask.DelayFrame(ReleaseFrames);
        }
    }
}
