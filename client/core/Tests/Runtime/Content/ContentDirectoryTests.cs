using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal.EditorTests.Content;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // A built content directory registered in Play Mode over a store: its archive read in place with the index next to it
    // (LocalContentStore), and fetched into an archive of zeros (FetchedContentStore over a source copying ranges). The directory is built by the editor before
    // Play Mode (ContentDirectoryTestBuild); each test registers it on its own lifetime, unregistered after the test.
    [PrebuildSetup("Internal.EditorTests.ContentDirectoryTestBuild")]
    [PostBuildCleanup("Internal.EditorTests.ContentDirectoryTestBuild")]
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
        public IEnumerator FetchedStore_Registered_OneRequestRealRootsZerosElsewhere() => UniTask.ToCoroutine(async () =>
        {
            var source = new CopyContentRangeSource(ContentTestPaths.Archive);
            var store = new FetchedContentStore(source, Path.GetFullPath(FetchedFolder), ContentOptions.DefaultRangeGap);
            var directory = await ContentDirectory.Register(_lifetime, _index, store);

            var registration = _index.RootFiles.Select(file => file.Name).Append(_index.Manifest.Name).Append(ContentIndex.HeaderName).ToArray();
            CollectionAssert.IsSubsetOf(registration, source.Copied, "The header, the manifest and the root files before the registration");
            Assert.AreEqual(1, source.Requests, "They lie together at the start of the archive");
            Assert.AreEqual(_index.ManifestHash, File.ReadAllText(Path.Combine(FetchedFolder, ContentPaths.ManifestHashFile)));

            var fetched = File.ReadAllBytes(FetchedArchive);
            Assert.AreEqual(_index.ArchiveLength, fetched.Length, "The archive at its full length");

            foreach (var file in _index.Files.Where(file => source.Copied.Contains(file.Name) == false))
                Assert.IsTrue(Span(fetched, file).All(value => value == 0), $"{file.Name} is zeros before its fetch");

            Assert.IsNotNull(directory.RootAssets<ContentTestRoot>().Single());
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator FetchedStore_TargetFetched_LoadsIt() => UniTask.ToCoroutine(async () =>
        {
            var source = new CopyContentRangeSource(ContentTestPaths.Archive);
            var store = new FetchedContentStore(source, Path.GetFullPath(FetchedFolder), ContentOptions.DefaultRangeGap);
            var directory = await ContentDirectory.Register(_lifetime, _index, store);
            var root = directory.RootAssets<ContentTestRoot>().Single();
            var files = _index.ObjectFiles(_targetGuid, 0);
            Assert.IsFalse(store.IsReal(files), "The target is zeros after the registration");

            var reported = 0L;
            await directory.FetchObject(_lifetime, _targetGuid, 0, new SyncProgress(bytes => reported = bytes));

            var built = File.ReadAllBytes(ContentTestPaths.Archive);
            var fetched = File.ReadAllBytes(FetchedArchive);

            foreach (var file in files)
                CollectionAssert.AreEqual(Span(built, file), Span(fetched, file), $"{file.Name} is real after the fetch");

            Assert.AreEqual(files.Sum(file => file.Size), reported, "The progress ends at the bytes of the files");

            // Real now: a second fetch requests nothing.
            var requests = source.Requests;
            await directory.FetchObject(_lifetime, _targetGuid, 0, null);
            Assert.AreEqual(requests, source.Requests);

            var target = root.Target.Load();
            Assert.IsNotNull(target, $"The target of the root ({root.Target.Status})");
            root.Target.Release();
            LogAssert.NoUnexpectedReceived();
        });

        private static string FetchedArchive => Path.Combine(FetchedFolder, ContentPaths.ArchiveFile);

        private static byte[] Span(byte[] archive, ContentFile file)
        {
            return archive.Skip((int)file.Offset).Take((int)file.Size).ToArray();
        }
    }
}
