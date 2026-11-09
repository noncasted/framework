using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // One download per file of a fetched content directory: callers that need one file wait for one download, which is
    // aborted only when every waiting lifetime has ended; the caller of an ended lifetime gets OperationCanceledException,
    // the file stays zeros and nothing is logged.
    public class ContentFileFetchesTests
    {
        private static readonly ContentFile Shared = new("shared.cf", 16);
        private static readonly ContentFile Other = new("other.cf", 8);

        private string _folder;
        private GatedContentFileSource _source;
        private ContentFileFetches _fetches;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Application.temporaryCachePath, nameof(ContentFileFetchesTests));

            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);

            Directory.CreateDirectory(_folder);
            File.WriteAllBytes(PathOf(Shared), new byte[Shared.Size]);
            File.WriteAllBytes(PathOf(Other), new byte[Other.Size]);

            _source = new GatedContentFileSource();
            _fetches = new ContentFileFetches(_source, _folder);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_folder, true);
        }

        [UnityTest]
        public IEnumerator TwoFetchesOfOneFile_DownloadOnce() => UniTask.ToCoroutine(async () =>
        {
            var first = new Lifetime();
            var second = new Lifetime();

            var a = _fetches.Fetch(first, new[] { Shared, Other }, null).Preserve();
            var b = _fetches.Fetch(second, new[] { Shared }, null).Preserve();

            CollectionAssert.AreEqual(new[] { Shared.Name, Other.Name }, _source.Started);
            Assert.AreEqual(2, _fetches.Running);

            _source.Open(Shared);
            await b;
            Assert.IsFalse(a.Status.IsCompleted(), "The first waits for its other file");

            _source.Open(Other);
            await a;

            CollectionAssert.AreEqual(new[] { Shared.Name, Other.Name }, _source.Started, "One download per file");
            Assert.IsTrue(_fetches.IsReal(Shared) && _fetches.IsReal(Other));
            Assert.AreEqual(0, _fetches.Running);
            Assert.IsTrue(File.ReadAllBytes(PathOf(Shared)).All(value => value == GatedContentFileSource.Value));

            // Real files are not fetched again.
            await _fetches.Fetch(new Lifetime(), new[] { Shared }, null);
            Assert.AreEqual(2, _source.Started.Count);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator LifetimeEnds_CallerCanceled_DownloadAbortedFileZeros() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var fetch = _fetches.Fetch(lifetime, new[] { Shared }, null);

            lifetime.Terminate();

            await AssertCanceled(fetch);
            CollectionAssert.AreEqual(new[] { Shared.Name }, _source.Aborted, "Nobody else needs the file: aborted");
            Assert.IsFalse(_fetches.IsReal(Shared));
            Assert.AreEqual(0, _fetches.Running);
            Assert.IsTrue(File.ReadAllBytes(PathOf(Shared)).All(value => value == 0), "The file stays zeros");

            // The next load fetches it anew.
            var again = _fetches.Fetch(new Lifetime(), new[] { Shared }, null).Preserve();
            Assert.AreEqual(2, _source.Started.Count);
            _source.Open(Shared);
            await again;
            Assert.IsTrue(_fetches.IsReal(Shared));
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator OneOfTwoWaitersEnds_DownloadGoesOn() => UniTask.ToCoroutine(async () =>
        {
            var leaving = new Lifetime();
            var staying = new Lifetime();

            var left = _fetches.Fetch(leaving, new[] { Shared }, null);
            var stayed = _fetches.Fetch(staying, new[] { Shared }, null).Preserve();

            leaving.Terminate();
            await AssertCanceled(left);
            Assert.IsEmpty(_source.Aborted, "Another lifetime still waits for the file");

            _source.Open(Shared);
            await stayed;

            Assert.AreEqual(1, _source.Started.Count);
            Assert.IsTrue(_fetches.IsReal(Shared));
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator EndedLifetime_Throws_StartsNothing() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            lifetime.Terminate();

            await AssertCanceled(_fetches.Fetch(lifetime, new[] { Shared }, null));
            Assert.IsEmpty(_source.Started);
        });

        private static async UniTask AssertCanceled(UniTask task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Assert.Fail("Expected OperationCanceledException");
        }

        private string PathOf(ContentFile file)
        {
            return Path.Combine(_folder, file.Name);
        }
    }
}
