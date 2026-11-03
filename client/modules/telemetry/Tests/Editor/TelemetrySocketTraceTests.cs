using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // The telemetry sends a trace and must not write itself into one: on the first phone a batch every 2 s became a span of the
    // startup trace. The real writer: everything here ends before the next frame, so before a request is serialized.
    public class TelemetrySocketTraceTests
    {
        private static readonly Guid _id = new Guid(5, 0, 0, new byte[8]);

        [SetUp]
        public void SetUp()
        {
            GameProfiler.Begin(nameof(TelemetrySocketTraceTests));
        }

        [TearDown]
        public void TearDown()
        {
            GameProfiler.Finish();
            File.Delete(ProfilerTraceStorage.LastPath);
        }

        [UnityTest]
        public IEnumerator EveryRequest_IsNoSpanOfTheTrace() => UniTask.ToCoroutine(async () =>
        {
            var writer = new ConnectionWriter(new NetworkTraffic());
            var lifetime = new Lifetime();
            writer.Run(lifetime, new SilentWebSocket());
            var transport = new TelemetrySocketTransport(new FakeTelemetrySocket(writer), new TelemetryTicks(), new TelemetryRoundTrip());

            var start = transport.Start(new TelemetrySessionStart(new[] { "menu" }, new TelemetryDevice(cores: 4), Guid.Empty));
            var send = transport.Send(_id, new TelemetryBatch(
                new[] { new TelemetryBatchName("a", null) },
                Array.Empty<TelemetryBatchSample>(),
                Array.Empty<TelemetryBatchMark>(),
                Array.Empty<TelemetryBatchError>()));
            var end = transport.End(_id);
            var startup = transport.SendStartup(new TelemetryStartupTrace(
                Guid.Empty, new TelemetryDevice(cores: 4), "Startup", false, 0d, 0, Array.Empty<TelemetryStartupSpan>()));

            var spans = ((Profiler)GameProfiler.Current).Snapshot().Spans.Select(span => span.Name).ToArray();

            lifetime.Terminate();
            await UniTask.WhenAll(start.AsUniTask(), send.AsUniTask(), end, startup.AsUniTask());

            CollectionAssert.IsEmpty(spans);
        });
    }
}
