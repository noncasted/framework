using System;
using System.Collections;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // One call of the HTTP transport against one answer of the server: the path, the link in every request, the answers, and a
    // failure of the network as Failed with a new link.
    public class TelemetryHttpTransportTests
    {
        private static readonly Guid _id = new Guid(5, 0, 0, new byte[8]);
        private static readonly TimeSpan _limit = TimeSpan.FromSeconds(5);

        private static TelemetrySessionStart Start()
        {
            return new TelemetrySessionStart(new[] { "menu" }, new TelemetryDevice(cores: 4), Guid.Empty);
        }

        private static TelemetryBatch Batch()
        {
            return new TelemetryBatch(
                new[] { new TelemetryBatchName("a", null) },
                Array.Empty<TelemetryBatchSample>(),
                Array.Empty<TelemetryBatchMark>(),
                Array.Empty<TelemetryBatchError>());
        }

        private static TelemetryStartupTrace Trace()
        {
            return new TelemetryStartupTrace(Guid.Empty, new TelemetryDevice(), "Startup", false, 10, 1, Array.Empty<TelemetryStartupSpan>());
        }

        private static Guid LinkOf(TelemetryHttpTransport transport)
        {
            return ((TelemetryHttpLink)transport.Link).Id;
        }

        [UnityTest]
        public IEnumerator Start_Enabled_TheIdOfTheServer() => UniTask.ToCoroutine(async () =>
        {
            var http = new FakeTelemetryHttp(_ => $"{{\"enabled\":true,\"sessionId\":\"{_id}\"}}");
            var transport = new TelemetryHttpTransport(http);

            var answer = await transport.Start(Start()).Timeout(_limit);

            Assert.AreEqual(TelemetrySendOutcome.Accepted, answer.Outcome);
            Assert.AreEqual(_id, answer.SessionId);
            Assert.AreEqual("/start", http.Requests[0].Path);
            var sent = JsonUtility.FromJson<TelemetryHttpSessionStart>(http.Requests[0].Json);
            Assert.AreEqual(LinkOf(transport), Guid.Parse(sent.linkId));
            Assert.AreEqual(4, sent.device.cores);
        });

        [UnityTest]
        public IEnumerator Start_NotEnabled_Disabled() => UniTask.ToCoroutine(async () =>
        {
            var transport = new TelemetryHttpTransport(new FakeTelemetryHttp(_ => "{\"enabled\":false}"));

            var answer = await transport.Start(Start()).Timeout(_limit);

            Assert.AreEqual(TelemetrySendOutcome.Disabled, answer.Outcome);
            Assert.AreEqual(Guid.Empty, answer.SessionId);
        });

        [UnityTest]
        public IEnumerator Send_TheSameLink_AcceptedThenDisabled() => UniTask.ToCoroutine(async () =>
        {
            var answers = new[] { "{\"enabled\":true}", "{\"enabled\":false}" };
            var calls = 0;
            var http = new FakeTelemetryHttp(_ => answers[calls++]);
            var transport = new TelemetryHttpTransport(http);
            var link = transport.Link;

            Assert.AreEqual(TelemetrySendOutcome.Accepted, await transport.Send(_id, Batch()).Timeout(_limit));
            Assert.AreEqual(TelemetrySendOutcome.Disabled, await transport.Send(_id, Batch()).Timeout(_limit));

            Assert.AreSame(link, transport.Link);
            var sent = JsonUtility.FromJson<TelemetryHttpBatch>(http.Requests[1].Json);
            Assert.AreEqual("/batch", http.Requests[1].Path);
            Assert.AreEqual(_id, Guid.Parse(sent.sessionId));
            Assert.AreEqual(LinkOf(transport), Guid.Parse(sent.linkId));
        });

        [UnityTest]
        public IEnumerator End_ThePathOfTheSession() => UniTask.ToCoroutine(async () =>
        {
            var http = new FakeTelemetryHttp(_ => "{}");
            var transport = new TelemetryHttpTransport(http);

            await transport.End(_id).Timeout(_limit);

            Assert.AreEqual("/end", http.Requests[0].Path);
            Assert.AreEqual(_id, Guid.Parse(JsonUtility.FromJson<TelemetryHttpSessionEnd>(http.Requests[0].Json).sessionId));
        });

        [UnityTest]
        public IEnumerator Startup_Enabled_TheIdOfTheServer() => UniTask.ToCoroutine(async () =>
        {
            var http = new FakeTelemetryHttp(_ => $"{{\"enabled\":true,\"startupId\":\"{_id}\"}}");
            var transport = new TelemetryHttpTransport(http);

            var answer = await transport.SendStartup(Trace()).Timeout(_limit);

            Assert.AreEqual(TelemetrySendOutcome.Accepted, answer.Outcome);
            Assert.AreEqual(_id, answer.StartupId);
            Assert.AreEqual("/startup", http.Requests[0].Path);
        });

        // The boundary of the network: a warning, Failed, and the next request goes on a new link, as after a lost socket.
        [UnityTest]
        public IEnumerator NetworkFailure_FailedOnANewLink() => UniTask.ToCoroutine(async () =>
        {
            var calls = 0;
            var http = new FakeTelemetryHttp(_ => calls++ == 0 ? throw new TelemetryHttpException(0, "timeout") : "{\"enabled\":true}");
            var transport = new TelemetryHttpTransport(http);
            var first = transport.Link;
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Telemetry\] /batch failed: timeout"));

            Assert.AreEqual(TelemetrySendOutcome.Failed, await transport.Send(_id, Batch()).Timeout(_limit));
            Assert.AreNotSame(first, transport.Link);
            Assert.AreEqual(TelemetrySendOutcome.Accepted, await transport.Send(_id, Batch()).Timeout(_limit));

            var before = Guid.Parse(JsonUtility.FromJson<TelemetryHttpBatch>(http.Requests[0].Json).linkId);
            var after = Guid.Parse(JsonUtility.FromJson<TelemetryHttpBatch>(http.Requests[1].Json).linkId);
            Assert.AreNotEqual(before, after);
            Assert.AreEqual(LinkOf(transport), after);
        });

        [UnityTest]
        public IEnumerator StatusOfTheServer_FailedOnEveryCall() => UniTask.ToCoroutine(async () =>
        {
            var transport = new TelemetryHttpTransport(new FakeTelemetryHttp(_ => throw new TelemetryHttpException(401, "status 401")));

            Assert.AreEqual(TelemetrySendOutcome.Failed, (await transport.Start(Start()).Timeout(_limit)).Outcome);
            Assert.AreEqual(TelemetrySendOutcome.Failed, await transport.Send(_id, Batch()).Timeout(_limit));
            Assert.AreEqual(TelemetrySendOutcome.Failed, (await transport.SendStartup(Trace()).Timeout(_limit)).Outcome);
            await transport.End(_id).Timeout(_limit);
        });

        // Not a failure of the network: an answer out of the contract is a bug, it goes up to the session, which writes its line.
        [UnityTest]
        public IEnumerator WrongAnswer_GoesUp() => UniTask.ToCoroutine(async () =>
        {
            var transport = new TelemetryHttpTransport(new FakeTelemetryHttp(_ => "{\"enabled\":true,\"sessionId\":\"broken\"}"));
            var caught = false;

            try
            {
                await transport.Start(Start()).Timeout(_limit);
            }
            catch (FormatException)
            {
                caught = true;
            }

            Assert.IsTrue(caught);
        });
    }
}
