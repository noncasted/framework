using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal.EditorTests.Content;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // A built content directory registered in Play Mode over a store: read in place (LocalContentStore), and fetched
    // into a folder of zeros (FetchedContentStore over a copying source). The directory is built by the editor before
    // Play Mode (ContentDirectoryTestBuild); each test registers it on its own lifetime, unregistered after the test.
    [PrebuildSetup("Internal.EditorTests.ContentDirectoryTestBuild")]
    public class ContentDirectoryTests
    {
        private const string FetchedFolder = ContentTestPaths.Output + "/Fetched";

        private ContentIndex _index;
        private string _targetGuid;
        private Lifetime _lifetime;

        [SetUp]
        public void SetUp()
        {
            _index = ContentIndex.FromJson(File.ReadAllText(ContentTestPaths.Index));
            _targetGuid = File.ReadAllText(ContentTestPaths.TargetGuid);
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
        public IEnumerator LocalStore_Registered_LoadsRootAndTarget() => UniTask.ToCoroutine(async () =>
        {
            var store = new LocalContentStore(Path.GetFullPath(ContentTestPaths.Folder));
            var directory = await ContentDirectory.Register(_lifetime, _index, store);

            var root = directory.RootAssets<ContentTestRoot>().Single();
            await directory.FetchObject(_lifetime, _targetGuid, 0, null);
            var target = root.Target.Load();

            Assert.IsNotNull(target, $"The target of the root ({root.Target.Status})");
            Assert.AreEqual("Target", target.name);
            root.Target.Release();
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator FetchedStore_Registered_RealRootsZerosElsewhere() => UniTask.ToCoroutine(async () =>
        {
            var source = new CopyContentFileSource(ContentTestPaths.Folder);
            var store = new FetchedContentStore(source, Path.GetFullPath(FetchedFolder));
            var directory = await ContentDirectory.Register(_lifetime, _index, store);

            var registration = _index.RootFiles.Select(file => file.Name).Append(_index.Manifest.Name).ToArray();
            CollectionAssert.AreEquivalent(registration, source.Copied, "Only the manifest and the root files before the registration");
            Assert.AreEqual(_index.ManifestHash, File.ReadAllText(Path.Combine(FetchedFolder, ContentPaths.ManifestHashFile)));

            foreach (var file in _index.Files.Where(file => registration.Contains(file.Name) == false))
            {
                var bytes = File.ReadAllBytes(Path.Combine(FetchedFolder, file.Name));
                Assert.AreEqual(file.Size, bytes.Length, $"The size of the zeros of {file.Name}");
                Assert.IsTrue(bytes.All(value => value == 0), $"{file.Name} is zeros before its fetch");
            }

            Assert.IsNotNull(directory.RootAssets<ContentTestRoot>().Single());
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator FetchedStore_TargetFetched_LoadsIt() => UniTask.ToCoroutine(async () =>
        {
            var source = new CopyContentFileSource(ContentTestPaths.Folder);
            var store = new FetchedContentStore(source, Path.GetFullPath(FetchedFolder));
            var directory = await ContentDirectory.Register(_lifetime, _index, store);
            var root = directory.RootAssets<ContentTestRoot>().Single();
            var before = source.Copied.Count;

            var reported = 0L;
            await directory.FetchObject(_lifetime, _targetGuid, 0, new SyncProgress(bytes => reported = bytes));
            var files = _index.ObjectFiles(_targetGuid, 0);

            foreach (var file in files)
            {
                CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(ContentTestPaths.Folder, file.Name)),
                    File.ReadAllBytes(Path.Combine(FetchedFolder, file.Name)), $"{file.Name} is real after the fetch");
            }

            Assert.AreEqual(files.Sum(file => file.Size), reported, "The progress ends at the bytes of the files");

            // The files of the root closure are real already, a second fetch copies nothing.
            var copied = source.Copied.Count;
            Assert.AreEqual(files.Count(file => _index.RootFiles.Contains(file) == false), copied - before);
            await directory.FetchObject(_lifetime, _targetGuid, 0, null);
            Assert.AreEqual(copied, source.Copied.Count);

            var target = root.Target.Load();
            Assert.IsNotNull(target, $"The target of the root ({root.Target.Status})");
            root.Target.Release();
            LogAssert.NoUnexpectedReceived();
        });
    }
}
