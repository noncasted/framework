using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests.Transport
{
    // The startup trace: a record, its link from the sessions, its final, its life on one socket.
    public partial class TelemetryTransportEndToEndTests
    {
        private static ProfilerSpanData Span(int parent, string name, double start, double duration, int frames = 1, bool unfinished = false, long alloc = 0L)
        {
            return new ProfilerSpanData { ParentId = parent, Name = name, StartMs = start, DurationMs = duration, Frames = frames, Unfinished = unfinished, AllocBytes = alloc };
        }

        private static ProfilerTraceData Snapshot()
        {
            return TelemetryScriptedStartup.Trace(
                "Startup",
                1500.5d,
                60,
                Span(-1, "Root", 0d, 1500.5d, 60, true, 2048L),
                Span(0, "GET https://api.example.com/v1/user?token=abc123", 10.25d, 300d, 12, false, 512L),
                Span(1, "Parse", 20d, 100d, 4));
        }

        private static ProfilerTraceData Final()
        {
            return TelemetryScriptedStartup.Trace(
                "Startup",
                2500d,
                90,
                Span(-1, "Root", 0d, 2500d, 90),
                Span(0, "Load", 5d, 1000d, 30),
                Span(0, "Show", 1005d, 1400d, 50),
                Span(2, "Menu", 1100d, 800d, 20));
        }

        private static async UniTask CheckRecord(ProfilerTraceData sent, Guid id, bool isFinal)
        {
            var record = await TelemetryTransportHost.Read("startups/" + id);
            var row = record["startup"];

            Assert.AreEqual(id.ToString(), row.Value<string>("id"));
            Assert.AreEqual(isFinal, row.Value<bool>("final"));
            Assert.AreEqual(sent.Name, row.Value<string>("name"));
            Assert.AreEqual(sent.DurationMs, row.Value<double>("durationMs"));
            Assert.AreEqual(sent.Frames, row.Value<int>("frames"));
            Assert.AreEqual(0, row.Value<int>("rejectedSpans"));
            Assert.AreEqual(0, row.Value<int>("droppedSpans"));

            var spans = (JArray)record["spans"];
            Assert.AreEqual(sent.Spans.Count, spans.Count);

            for (var i = 0; i < spans.Count; i++)
            {
                Assert.AreEqual(sent.Spans[i].ParentId, spans[i].Value<int>("parentIndex"), $"parent of the span {i}");
                Assert.AreEqual(TelemetryScrubber.Scrub(sent.Spans[i].Name), spans[i].Value<string>("name"), $"name of the span {i}");
                Assert.AreEqual(sent.Spans[i].StartMs, spans[i].Value<double>("startMs"), $"start of the span {i}");
                Assert.AreEqual(sent.Spans[i].DurationMs, spans[i].Value<double>("durationMs"), $"duration of the span {i}");
                Assert.AreEqual(sent.Spans[i].Frames, spans[i].Value<int>("frames"), $"frames of the span {i}");
                Assert.AreEqual(sent.Spans[i].Unfinished, spans[i].Value<bool>("unfinished"), $"unfinished of the span {i}");
                Assert.AreEqual(sent.Spans[i].AllocBytes, spans[i].Value<long>("allocBytes"), $"alloc of the span {i}");
            }
        }

        [UnityTest]
        public IEnumerator Startup_BeforeTheSession_RecordLinkedAndReplacedByTheFinal() => UniTask.ToCoroutine(async () =>
        {
            var device = new TelemetryDevice("web", "1.2.3", "Chrome", 120, "Android", 14, "Adreno 650", 8, 4d, 1280, 720, 2d);
            var client = Client(device);
            var tag = Tag("startup");
            var lifetime = NewLifetime();
            await client.Connect();

            var snapshot = Snapshot();
            client.StartupSource.Open(snapshot);
            client.Telemetry.SendStartupTrace();
            client.Telemetry.StartSession(lifetime, tag);

            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count == 1).Timeout(TimeSpan.FromSeconds(15));

            var startup = client.Transport.Startups.Single();
            Assert.AreEqual(TelemetrySendOutcome.Accepted, startup.Answer.Outcome);
            Assert.AreEqual(startup.Answer.StartupId, client.Transport.Starts.Single().StartupId, "the session refers to the record");

            var id = startup.Answer.StartupId;
            await CheckRecord(snapshot, id, false);
            CheckDevice(device, (await TelemetryTransportHost.Read("startups/" + id))["startup"]["device"]);
            Assert.AreEqual(id.ToString(), (await TelemetryTransportHost.Session(tag)).Value<string>("startupId"));

            var final = Final();
            client.StartupSource.Close(final);
            await UniTask.WaitUntil(() => client.Transport.Startups.Count == 2).Timeout(TimeSpan.FromSeconds(15));

            Assert.AreEqual(id, client.Transport.Startups[1].Answer.StartupId, "the final replaces the record");
            await CheckRecord(final, id, true);

            var linked = (await TelemetryTransportHost.Read("startups/" + id))["sessions"];
            Assert.AreEqual(client.Transport.StartAnswers.Single().SessionId.ToString(), linked.Single().Value<string>("id"));
            Assert.AreEqual(1, ((JArray)await TelemetryTransportHost.Read($"sessions?startupId={id}")).Count);

            lifetime.Terminate();
            await TelemetryTransportHost.UntilClosed(tag, "ended");
            Assert.AreEqual(id.ToString(), (await TelemetryTransportHost.Session(tag)).Value<string>("startupId"), "the link survives the close");
        });

        // The same connection run again: the id of the old run is gone, a session refers to no old record.
        [UnityTest]
        public IEnumerator Startup_Reconnect_NewRecordOldIdNotUsed() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            await client.Connect();

            client.StartupSource.Open(Snapshot());
            client.Telemetry.SendStartupTrace();
            await UniTask.WaitUntil(() => client.Transport.Startups.Count == 1).Timeout(TimeSpan.FromSeconds(15));

            var old = client.Transport.Startups[0].Answer.StartupId;
            Assert.AreNotEqual(Guid.Empty, old);

            client.Connection.Drop("the test breaks it");
            await UniTask.WaitUntil(() => client.Connection.Writer.IsClosed).Timeout(TimeSpan.FromSeconds(15));
            await client.Reconnect();

            // No trace asked for on the new run: the session has no record.
            var withoutTag = Tag("noreport");
            var without = NewLifetime();
            client.Telemetry.StartSession(without, withoutTag);
            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count == 1).Timeout(TimeSpan.FromSeconds(15));
            Assert.AreEqual(Guid.Empty, client.Transport.Starts[0].StartupId);
            Assert.AreEqual(JTokenType.Null, (await TelemetryTransportHost.Session(withoutTag))["startupId"].Type);
            without.Terminate();

            // Asked for: a new record, and the old one stays as a snapshot.
            var tag = Tag("report");
            client.Telemetry.SendStartupTrace();
            client.Telemetry.StartSession(NewLifetime(), tag);
            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count == 2).Timeout(TimeSpan.FromSeconds(15));

            var fresh = client.Transport.Startups[1].Answer.StartupId;
            Assert.AreEqual(TelemetrySendOutcome.Accepted, client.Transport.Startups[1].Answer.Outcome);
            Assert.AreEqual(Guid.Empty, client.Transport.Startups[1].Trace.StartupId, "a new record is asked for");
            Assert.AreNotEqual(old, fresh);
            Assert.AreEqual(fresh, client.Transport.Starts[1].StartupId);
            Assert.AreEqual(fresh.ToString(), (await TelemetryTransportHost.Session(tag)).Value<string>("startupId"));
            Assert.IsFalse((await TelemetryTransportHost.Read("startups/" + old))["startup"].Value<bool>("final"));
            CollectionAssert.IsEmpty((await TelemetryTransportHost.Read("startups/" + old))["sessions"]);
        });

        [UnityTest]
        public IEnumerator Startup_FlagOff_NoRecordAndNoLinkLater() => UniTask.ToCoroutine(async () =>
        {
            await TelemetryTransportHost.SetFlag(false);

            var client = Client();
            var records = ((JArray)await TelemetryTransportHost.Read("startups?limit=1000")).Count;
            await client.Connect();

            client.StartupSource.Open(Snapshot());
            client.Telemetry.SendStartupTrace();
            await UniTask.WaitUntil(() => client.Transport.Startups.Count == 1).Timeout(TimeSpan.FromSeconds(15));

            Assert.AreEqual(TelemetrySendOutcome.Disabled, client.Transport.Startups[0].Answer.Outcome);
            Assert.AreEqual(records, ((JArray)await TelemetryTransportHost.Read("startups?limit=1000")).Count);

            // The flag comes back: this connection sends no more traces, its sessions go on without a record.
            await TelemetryTransportHost.SetFlag(true);

            var tag = Tag("offstartup");
            client.Telemetry.SendStartupTrace();
            client.Telemetry.StartSession(NewLifetime(), tag);
            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count == 1).Timeout(TimeSpan.FromSeconds(15));

            Assert.AreEqual(1, client.Transport.Startups.Count, "no second trace on this socket");
            Assert.AreEqual(JTokenType.Null, (await TelemetryTransportHost.Session(tag))["startupId"].Type);
            Assert.AreEqual(records, ((JArray)await TelemetryTransportHost.Read("startups?limit=1000")).Count);
        });
    }
}
