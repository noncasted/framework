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
    // Scopes over the content directory of ContentDirectoryTestBuild: the scene of a scope (WithAssetScene) and its
    // content (LoadContent, RequestContent) are loaded on the lifetime of the scope and go with its dispose; a child takes
    // the loader of its root, the one the root registers or AddContent creates.
    [PrebuildSetup("Internal.EditorTests.ContentDirectoryTestBuild")]
    [PostBuildCleanup("Internal.EditorTests.ContentDirectoryTestBuild")]
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
        }

        [UnityTest]
        public IEnumerator RegisteredDirectory_ScopeLoadsSceneAndContent_DisposeReleases() => UniTask.ToCoroutine(async () =>
        {
            var index = ContentIndex.FromJson(File.ReadAllText(ContentTestPaths.Index));
            var directory = await ContentDirectory.Register(_lifetime, index, new LocalContentStore(Path.GetFullPath(ContentTestPaths.Folder)));
            var references = directory.RootAssets<ContentTestReferences>().Single();
            var root = await new ServiceScopeLoader().Load(ScopeLoadOptions.Create(null, ContentScopeRoots.Root.Construct, new ContentLoader(directory)));

            await AssertChild(root, references);

            await root.Dispose();
            LogAssert.NoUnexpectedReceived();
        });

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AddContent_EditorDirectory_ChildLoadsTheProjectAssets() => UniTask.ToCoroutine(async () =>
        {
            var references = AssetDatabase.LoadAssetAtPath<ContentTestReferences>(ContentTestPaths.ReferencesAsset);
            var previous = EditorContentDirectories.Install(name => new EditorContentDirectory(name, new Object[] { references }));

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
                EditorContentDirectories.Install(previous);
            }

            LogAssert.NoUnexpectedReceived();
        });
#endif

#if UNITY_EDITOR
        // The root registers a second directory next to the one of the catalogues: each has its own loader, an object of
        // the second loads from a reference of its roots and goes with the root.
        [UnityTest]
        public IEnumerator AddContentDirectory_SecondDirectory_LoadsFromItsRoots() => UniTask.ToCoroutine(async () =>
        {
            var main = await Register(ContentTestPaths.Index, ContentTestPaths.Folder);
            var extra = await Register(ContentTestPaths.ExtraIndex, ContentTestPaths.ExtraFolder);
            var previous = EditorContentDirectories.Install(name => name == ContentTestPaths.Name ? main : name == ContentTestPaths.ExtraName ? extra :
                throw new InvalidOperationException($"No test directory {name}"));

            try
            {
                var options = new[] { new ContentOptions(ContentTestPaths.Name), new ContentOptions(ContentTestPaths.ExtraName) };
                var root = await new ServiceScopeLoader().Load(ScopeLoadOptions.Create(null, ContentScopeRoots.TwoDirectories.Construct, options));
                var directories = root.Resolve<IContentDirectories>();
                var loader = directories.Get(ContentTestPaths.ExtraName);
                var references = loader.RootAssets<ContentTestReferences>().Single();

                Assert.AreSame(root.Resolve<IContentLoader>(), directories.Get(ContentTestPaths.Name), "The loader of AddContent");
                Assert.AreNotSame(root.Resolve<IContentLoader>(), loader);
                Assert.IsEmpty(root.Resolve<IContentLoader>().RootAssets<ContentTestReferences>().Where(asset => asset.name == "ExtraReferences"));

                var target = await loader.Load(root.Lifetime, references.Target);

                Assert.AreEqual("ExtraTarget", target.name);
                Assert.AreEqual(loader.Progress(references.Target).Value.Total, loader.Progress(references.Target).Value.Loaded, "Its files are real");
                Assert.Throws<InvalidOperationException>(() => directories.Get("Missing"));

                var scene = await loader.LoadScene(root.Lifetime, references.Scene);
                Assert.IsTrue(scene.Scene.isLoaded, "A scene of the second directory");

                await root.Dispose();
                Assert.IsFalse(references.Target.IsLoaded, "Released with the root");
            }
            finally
            {
                EditorContentDirectories.Install(previous);
            }

            await UniTask.DelayFrame(ReleaseFrames);
            LogAssert.NoUnexpectedReceived();
        });

        private async UniTask<ContentDirectory> Register(string index, string folder)
        {
            return await ContentDirectory.Register(_lifetime, ContentIndex.FromJson(File.ReadAllText(index)), new LocalContentStore(Path.GetFullPath(folder)));
        }
#endif

        [UnityTest]
        public IEnumerator AddContentDirectory_WithoutAddContent_Throws() => UniTask.ToCoroutine(async () =>
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
            StringAssert.Contains("AddContentDirectory needs AddContent", caught.Message);
        });

        [UnityTest]
        public IEnumerator RootWithoutContent_ContentScene_Throws() => UniTask.ToCoroutine(async () =>
        {
            var options = new ScopeLoadOptions(null, ContentScopeRoots.Empty.Construct).WithAssetScene(new ContentScene(default, "none"));
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
