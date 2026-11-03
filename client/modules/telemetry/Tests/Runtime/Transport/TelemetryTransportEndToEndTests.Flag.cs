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
    // The flag of the host, changed as the Features page does it. And the server that never answers.
    public partial class TelemetryTransportEndToEndTests
    {
        [UnityTest]
        public IEnumerator FlagOff_Start_RefusedAndNoRecord() => UniTask.ToCoroutine(async () =>
        {
            await TelemetryTransportHost.SetFlag(false);

            var client = Client();
            var tag = Tag("off");
            var before = await TelemetryTransportHost.MessagesCount();
            await client.Connect();

            client.Collector.Script(sample => sample.Gauge("off.value", 1d));
            client.Telemetry.StartSession(NewLifetime(), tag);

            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count == 1).Timeout(TimeSpan.FromSeconds(15));
            await UniTask.Delay(500, DelayType.Realtime);

            Assert.AreEqual(TelemetrySendOutcome.Disabled, client.Transport.StartAnswers.Single().Outcome);
            CollectionAssert.IsEmpty(client.Transport.Sent, "no batch after a refused start");
            CollectionAssert.IsEmpty(client.Transport.Ends);
            Assert.AreEqual(before + 1, await TelemetryTransportHost.MessagesCount(), "only the start");
            Assert.IsNull(await TelemetryTransportHost.Session(tag));
        });

        [UnityTest]
        public IEnumerator FlagOffInASession_SendingStops_NextSessionAsksAgain() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            var tag = Tag("cut");
            await client.Connect();

            client.Collector.Script(sample => sample.Gauge("cut.value", 1d));
            client.Telemetry.StartSession(NewLifetime(), tag);
            await WaitBatches(client, 2);

            await TelemetryTransportHost.SetFlag(false);
            await UniTask.WaitUntil(() => client.Transport.Sent.Any(sent => sent.Outcome == TelemetrySendOutcome.Disabled)).Timeout(TimeSpan.FromSeconds(15));

            var sent = client.Transport.Sent.Count;
            var messages = await TelemetryTransportHost.MessagesCount();
            await UniTask.Delay(600, DelayType.Realtime);

            Assert.AreEqual(sent, client.Transport.Sent.Count, "the sending is over");
            Assert.AreEqual(messages, await TelemetryTransportHost.MessagesCount());
            CollectionAssert.IsEmpty(client.Transport.Ends);

            var row = await TelemetryTransportHost.UntilClosed(tag, "ended");
            var accepted = client.Transport.Sent.Where(item => item.Outcome == TelemetrySendOutcome.Accepted).Sum(item => item.Batch.Samples.Count);
            Assert.AreEqual(accepted, (await TelemetryTransportHost.ReadAll(row.Value<string>("id"), "samples")).Count, "the refused batch is not written");

            // Still off: the next session asks and gets the same answer.
            var offTag = Tag("cutoff");
            client.Telemetry.StartSession(NewLifetime(), offTag);
            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count == 2).Timeout(TimeSpan.FromSeconds(15));
            Assert.AreEqual(TelemetrySendOutcome.Disabled, client.Transport.StartAnswers[1].Outcome);
            Assert.IsNull(await TelemetryTransportHost.Session(offTag));

            // On again: the session after it goes on the same socket.
            await TelemetryTransportHost.SetFlag(true);

            var onTag = Tag("cuton");
            var lifetime = NewLifetime();
            client.Telemetry.StartSession(lifetime, onTag);
            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count == 3).Timeout(TimeSpan.FromSeconds(15));
            Assert.AreEqual(TelemetrySendOutcome.Accepted, client.Transport.StartAnswers[2].Outcome);
            await UniTask.WaitUntil(() => client.Transport.Sent.Count > sent).Timeout(TimeSpan.FromSeconds(15));
            lifetime.Terminate();

            var on = await TelemetryTransportHost.UntilClosed(onTag, "ended");
            await TelemetryReadCheck.ReadEqualsSent(client.Transport, Guid.Parse(on.Value<string>("id")));
        });

        // The real 20 s of the writer on a socket whose server takes everything and answers nothing. The socket stays open, so
        // nothing but the timer may end the wait.
        [UnityTest, Timeout(120000)]
        public IEnumerator SilentServer_StartFailsByTheTimeout_NextSessionGoes() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            var tag = Tag("silent");
            await client.Connect(TelemetryTransportHost.MuteRoute);

            var startedAt = DateTime.UtcNow;
            client.Collector.Script(sample => sample.Gauge("silent.value", 1d));
            client.Telemetry.StartSession(NewLifetime(), tag);

            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count == 1).Timeout(TimeSpan.FromSeconds(30));

            var seconds = (DateTime.UtcNow - startedAt).TotalSeconds;
            Assert.AreEqual(TelemetrySendOutcome.Failed, client.Transport.StartAnswers[0].Outcome);
            Assert.GreaterOrEqual(seconds, 19.5d, "the timer of the writer");
            Assert.Less(seconds, 25d, "the timer of the writer");
            Assert.IsFalse(client.Connection.Writer.IsClosed, "the socket is alive, only silent");
            CollectionAssert.IsEmpty(client.Transport.Sent);
            Assert.IsNull(await TelemetryTransportHost.Session(tag));

            // The next session of the same module, on a socket that answers: it does not wait for anything of the first one.
            await client.Connect();

            var nextTag = Tag("afterilent");
            var lifetime = NewLifetime();
            var nextStartedAt = DateTime.UtcNow;
            client.Telemetry.StartSession(lifetime, nextTag);

            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count == 2).Timeout(TimeSpan.FromSeconds(5));
            Assert.AreEqual(TelemetrySendOutcome.Accepted, client.Transport.StartAnswers[1].Outcome);
            Assert.Less((DateTime.UtcNow - nextStartedAt).TotalSeconds, 5d);

            await UniTask.WaitUntil(() => client.Transport.Sent.Count > 0).Timeout(TimeSpan.FromSeconds(15));
            lifetime.Terminate();

            await TelemetryTransportHost.UntilClosed(nextTag, "ended");
        });
    }
}
