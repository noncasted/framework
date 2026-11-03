using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // A request is a span of the running trace, unless its message asks not to be: a periodic request would fill the trace.
    public class ConnectionWriterTraceTests
    {
        [SetUp]
        public void SetUp()
        {
            GameProfiler.Begin(nameof(ConnectionWriterTraceTests));
        }

        [TearDown]
        public void TearDown()
        {
            GameProfiler.Finish();
            File.Delete(ProfilerTraceStorage.LastPath);
        }

        [UnityTest]
        public IEnumerator Request_IsASpanOfTheTrace() => UniTask.ToCoroutine(async () =>
        {
            var spans = await SpansOfRequest(new SharedSessionAuth.Request());

            CollectionAssert.Contains(spans, "Request: SharedSessionAuth.Request");
        });

        [UnityTest]
        public IEnumerator UnprofiledRequest_IsNoSpan() => UniTask.ToCoroutine(async () =>
        {
            var spans = await SpansOfRequest(new QuietRequest());

            CollectionAssert.IsEmpty(spans.Where(name => name.StartsWith("Request:")));
        });

        // The spans of the trace while the request waits for its answer; the end of the connection ends the request.
        private static async UniTask<string[]> SpansOfRequest(INetworkContext context)
        {
            var writer = new ConnectionWriter(new NetworkTraffic());
            var lifetime = new Lifetime();
            writer.Run(lifetime, new QuietSocket());

            var request = writer.WriteRequest<EmptyResponse>(context);
            var spans = ((Profiler)GameProfiler.Current).Snapshot().Spans.Select(span => span.Name).ToArray();

            lifetime.Terminate();

            var isCanceled = await request.SuppressCancellationThrow();
            Assert.IsTrue(isCanceled.IsCanceled);

            return spans;
        }

        private class QuietRequest : IUnprofiledRequest
        {
        }

        private class QuietSocket : IWebSocket
        {
            public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
            public IViewableDelegate<SocketCloseInfo> Closed { get; } = new ViewableDelegate<SocketCloseInfo>();

            public UniTask Connect() => UniTask.CompletedTask;

            public UniTask Send(byte[] bytes) => UniTask.CompletedTask;

            public void Drop(string reason)
            {
            }
        }
    }
}
