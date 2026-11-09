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
    // One request per range of the archive of a fetched content directory: callers that need one file wait for one
    // request, which is aborted only when every waiting lifetime has ended; the caller of an ended lifetime gets
    // OperationCanceledException, the bytes stay zeros and nothing is logged. Files at most the gap apart come in one
    // request, the files between them with it.
    public class ContentFileFetchesTests
    {
        private const long Gap = 16;
        private static readonly ContentFile Shared = new("shared.cf", 100, 16);
        private static readonly ContentFile Between = new("between.cf", 118, 8);
        private static readonly ContentFile Next = new("next.cf", 130, 8);
        private static readonly ContentFile Other = new("other.cf", 200, 8);

        private string _folder;
        private string _archive;
        private GatedContentRangeSource _source;
        private ContentFileFetches _fetches;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Application.temporaryCachePath, nameof(ContentFileFetchesTests));

            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);

            Directory.CreateDirectory(_folder);
            _archive = Path.Combine(_folder, ContentPaths.ArchiveFile);
            File.WriteAllBytes(_archive, new byte[Other.End]);

            _source = new GatedContentRangeSource();
            _fetches = new ContentFileFetches(_source, _archive, new ContentRanges(new[] { Shared, Between, Next, Other }, Gap));
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

            Assert.AreEqual(2, _source.Started.Count, "Too far apart for one request");
            Assert.AreEqual(2, _fetches.Running);

            _source.Open(Shared);
            await b;
            Assert.IsFalse(a.Status.IsCompleted(), "The first waits for its other file");

            _source.Open(Other);
            await a;

            Assert.AreEqual(2, _source.Started.Count, "One request per file");
            Assert.IsTrue(_fetches.IsReal(Shared) && _fetches.IsReal(Other));
            Assert.AreEqual(0, _fetches.Running);
            Assert.IsTrue(BytesOf(Shared).All(value => value == GatedContentRangeSource.Value));

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
            Assert.AreEqual(1, _source.Aborted.Count, "Nobody else needs the file: aborted");
            Assert.IsFalse(_fetches.IsReal(Shared));
            Assert.AreEqual(0, _fetches.Running);
            Assert.IsTrue(BytesOf(Shared).All(value => value == 0), "The file stays zeros");

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
        public IEnumerator NeighboursWithinTheGap_OneRequest_FileBetweenRealProgressOfTheAsked() => UniTask.ToCoroutine(async () =>
        {
            var reported = 0L;
            var fetch = _fetches.Fetch(new Lifetime(), new[] { Next, Shared, Shared }, new Reported(bytes => reported = bytes)).Preserve();

            Assert.AreEqual(1, _source.Started.Count);
            var range = _source.Started[0];
            Assert.AreEqual(Shared.Offset, range.Offset);
            Assert.AreEqual(Next.End, range.End);
            CollectionAssert.AreEqual(new[] { Shared, Between, Next }, range.Files);

            _source.Open(Shared);
            await fetch;

            Assert.IsTrue(_fetches.IsReal(Between), "Brought by the request of its neighbours");
            Assert.AreEqual(Shared.Size + Next.Size, reported, "The bytes of the files asked for, each once");

            // A file the request brought is not fetched again.
            await _fetches.Fetch(new Lifetime(), new[] { Between }, null);
            Assert.AreEqual(1, _source.Started.Count);
            LogAssert.NoUnexpectedReceived();
        });

        [UnityTest]
        public IEnumerator FileOfARunningRequest_WaitsForIt() => UniTask.ToCoroutine(async () =>
        {
            var first = _fetches.Fetch(new Lifetime(), new[] { Shared, Next }, null).Preserve();
            var second = _fetches.Fetch(new Lifetime(), new[] { Between }, null).Preserve();

            Assert.AreEqual(1, _source.Started.Count, "The file between is on its way already");
            _source.Open(Between);
            await second;
            await first;
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

        private byte[] BytesOf(ContentFile file)
        {
            return File.ReadAllBytes(_archive).Skip((int)file.Offset).Take((int)file.Size).ToArray();
        }
    
        private sealed class Reported : IProgress<long>
        {
            public Reported(Action<long> report)
            {
                _report = report;
            }

            private readonly Action<long> _report;

            public void Report(long value) => _report(value);
        }
    }
}
