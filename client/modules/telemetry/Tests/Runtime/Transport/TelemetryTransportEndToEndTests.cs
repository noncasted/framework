using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests.Transport
{
    // The real client over a real socket against the real host: what is proven is read back through the API. Run by
    // tools/scripts/test-telemetry-transport.sh; without the host every test is ignored and nothing is registered.
    //
    // Every test has its own tags, so what it reads is what it has written.
    public partial class TelemetryTransportEndToEndTests
    {
        private static int _number;

        private readonly List<string> _unexpectedLogs = new List<string>();
        private readonly List<TelemetryTransportClient> _clients = new List<TelemetryTransportClient>();
        private readonly List<Lifetime> _lifetimes = new List<Lifetime>();

        [UnitySetUp]
        public IEnumerator SetUp() => UniTask.ToCoroutine(async () =>
        {
            if (TelemetryTransportHost.IsPresent == false)
                Assert.Ignore("No host: tools/scripts/test-telemetry-transport.sh runs these");

            Application.logMessageReceivedThreaded += OnLog;
            await TelemetryTransportHost.SetFlag(true);
        });

        [UnityTearDown]
        public IEnumerator TearDown() => UniTask.ToCoroutine(async () =>
        {
            Application.logMessageReceivedThreaded -= OnLog;

            foreach (var lifetime in _lifetimes)
                lifetime.Terminate();

            foreach (var client in _clients)
                client.Dispose();

            _lifetimes.Clear();
            _clients.Clear();

            if (TelemetryTransportHost.IsPresent)
                await TelemetryTransportHost.SetFlag(true);

            var logs = _unexpectedLogs.ToArray();

            _unexpectedLogs.Clear();
            CollectionAssert.IsEmpty(logs, "errors in the log that no test has made");
        });

        private void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && message.Contains("E2E") == false)
            {
                lock (_unexpectedLogs)
                    _unexpectedLogs.Add(message);
            }
        }

        private static string Tag(string name)
        {
            return $"e2e.{name}.{Guid.NewGuid():N}.{++_number}".Substring(0, 40);
        }

        private TelemetryTransportClient Client(
            TelemetryDevice device = null,
            ITelemetryDeviceSource deviceSource = null,
            bool strictNames = true)
        {
            // The errors of a test go to the telemetry as well. Its own carry "E2E", any other one fails it in the tear down. Here: the
            // log scope of a test exists only in its body.
            LogAssert.ignoreFailingMessages = true;

            var client = new TelemetryTransportClient(device, deviceSource, strictNames);

            _clients.Add(client);

            return client;
        }

        private Lifetime NewLifetime()
        {
            var lifetime = new Lifetime();

            _lifetimes.Add(lifetime);

            return lifetime;
        }

        private static UniTask WaitBatches(TelemetryTransportClient client, int count)
        {
            return UniTask.WaitUntil(() => client.Transport.Sent.Count >= count).Timeout(TimeSpan.FromSeconds(15));
        }

        // Menu, run, menu on one socket: the real order of a game, with the data of each kept apart.
        [UnityTest]
        public IEnumerator MenuRunMenu_OneSocket_ThreeSessionsEachWithItsData() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            var sitting = Tag("sitting");
            await client.Connect();

            var firstMenu = await Part(client, sitting, "menu", "first", 1000d);
            var run = await Part(client, sitting, "run", "second", 2000d);
            var secondMenu = await Part(client, sitting, "menu", "third", 3000d, true);

            await UniTask.WaitUntil(() => client.Transport.Ends.Contains(secondMenu)).Timeout(TimeSpan.FromSeconds(15));

            var rows = (JArray)await TelemetryTransportHost.Read("sessions?limit=10&tag=" + sitting);
            CollectionAssert.AreEquivalent(new[] { firstMenu, run, secondMenu }.Select(id => id.ToString()), rows.Select(row => row.Value<string>("id")));

            await CheckPart(client, firstMenu, "menu", "first", 1000d, sitting);
            await CheckPart(client, run, "run", "second", 2000d, sitting);
            await CheckPart(client, secondMenu, "menu", "third", 3000d, sitting);

            Assert.AreEqual(2, ((JArray)await TelemetryTransportHost.Read($"sessions?limit=10&tag=menu&tag={sitting}")).Count);
            Assert.AreEqual(1, ((JArray)await TelemetryTransportHost.Read($"sessions?limit=10&tag=run&tag={sitting}")).Count);
        });

        // One part of the game: its metric, mark and error, closed by the start of the next part or, the last one, by its lifetime.
        private async UniTask<Guid> Part(TelemetryTransportClient client, string sitting, string tag, string name, double value, bool isLast = false)
        {
            var lifetime = NewLifetime();
            var answers = client.Transport.StartAnswers.Count;

            client.Collector.Script(sample => {
                sample.Gauge($"{name}.frame", value, "ms");
                sample.Gauge("shared.value", value + 1d);
            });
            client.Telemetry.StartSession(lifetime, tag, sitting);

            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count > answers).Timeout(TimeSpan.FromSeconds(15));

            var id = client.Transport.StartAnswers[answers].SessionId;

            client.Telemetry.Mark($"{name}.opened");
            Debug.LogError($"E2E broken in {name}");
            await UniTask.WaitUntil(() => client.Transport.Sent.Count(sent => sent.Session == id && sent.Batch.Errors.Count > 0) > 0).Timeout(TimeSpan.FromSeconds(15));

            if (isLast)
                lifetime.Terminate();

            return id;
        }

        private static async UniTask CheckPart(TelemetryTransportClient client, Guid id, string tag, string name, double value, string sitting)
        {
            var row = await TelemetryTransportHost.Until(() => TelemetryTransportHost.Read("sessions/" + id), read => read.Value<string>("status") != "live", "closed");

            Assert.AreEqual("ended", row.Value<string>("status"));
            CollectionAssert.AreEquivalent(new[] { tag, sitting }, row["tags"].Values<string>());
            CollectionAssert.AreEquivalent(new[] { $"{name}.frame", "shared.value" }, row["names"].Select(item => item.Value<string>("name")));

            var digest = row["digest"];
            Assert.AreEqual(JTokenType.Object, digest.Type);
            Assert.AreEqual(1, digest.Value<int>("marks"));
            Assert.AreEqual($"E2E broken in {name}", digest["errorGroups"]["groups"].Single().Value<string>("message"));

            var metrics = digest["summary"]["metrics"];
            Assert.AreEqual(value, metrics.Single(metric => metric.Value<string>("name") == $"{name}.frame").Value<double>("average"));
            Assert.AreEqual(value + 1d, metrics.Single(metric => metric.Value<string>("name") == "shared.value").Value<double>("average"));

            var marks = await TelemetryTransportHost.ReadAll(id.ToString(), "marks");
            Assert.AreEqual($"{name}.opened", marks.Single().Value<string>("name"));

            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);
        }

        // A socket that breaks under an open session: the server closes it as lost, with what has come.
        [UnityTest]
        public IEnumerator SocketLost_OpenSession_LostWithLogAndDigest() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            var tag = Tag("lost");
            await client.Connect();

            client.Collector.Script(sample => sample.Gauge("lost.value", 7d, "count"));
            client.Telemetry.StartSession(NewLifetime(), tag);
            client.Telemetry.Mark("lost.mark");
            Debug.LogError("E2E broken before the loss");
            await UniTask.WaitUntil(() => client.Transport.Sent.Any(sent => sent.Batch.Errors.Count > 0)).Timeout(TimeSpan.FromSeconds(15));

            client.Connection.Drop("the test breaks it");

            var row = await TelemetryTransportHost.UntilClosed(tag, "lost");
            var id = row.Value<string>("id");
            var card = await TelemetryTransportHost.Read("sessions/" + id);

            Assert.AreEqual(JTokenType.Object, card["digest"].Type, "the digest of a lost session");
            Assert.AreEqual(7d, card["digest"]["summary"]["metrics"].Single().Value<double>("average"));
            Assert.AreEqual("E2E broken before the loss", card["digest"]["errorGroups"]["groups"].Single().Value<string>("message"));
            Assert.IsNotEmpty(await TelemetryTransportHost.ReadAll(id, "samples"));
            Assert.AreEqual("lost.mark", (await TelemetryTransportHost.ReadAll(id, "marks")).Single().Value<string>("name"));
            Assert.AreEqual(1, (await TelemetryTransportHost.ReadAll(id, "errors")).Count);
            CollectionAssert.IsEmpty(client.Transport.Ends);

            // The sending is over on the client too: what was accepted before the loss is what is read.
            var sentSamples = client.Transport.Sent.Where(sent => sent.Outcome == TelemetrySendOutcome.Accepted).Sum(sent => sent.Batch.Samples.Count);
            Assert.AreEqual(sentSamples, (await TelemetryTransportHost.ReadAll(id, "samples")).Count);
        });

        // Nothing goes outside a session, whatever the game does around it.
        [UnityTest]
        public IEnumerator NoSession_NothingOnTheServer() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            var before = await TelemetryTransportHost.MessagesCount();
            var sessions = ((JArray)await TelemetryTransportHost.Read("sessions?limit=1000")).Count;
            await client.Connect();

            client.Collector.Script(sample => sample.Gauge("idle.value", 1d));
            client.Telemetry.Mark("idle.mark");
            Debug.LogError("E2E broken outside a session");
            await UniTask.Delay(1000, DelayType.Realtime);

            Assert.AreEqual(before, await TelemetryTransportHost.MessagesCount());
            Assert.AreEqual(sessions, ((JArray)await TelemetryTransportHost.Read("sessions?limit=1000")).Count);
            CollectionAssert.IsEmpty(client.Transport.Starts);
            CollectionAssert.IsEmpty(client.Transport.Sent);
        });

        // Many batches: names only in the first and with a late metric, gaps in the values; nothing moves to a wrong slot.
        [UnityTest]
        public IEnumerator LongSession_NamesOnceThenValues_NothingMoves() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            var tag = Tag("long");
            var lifetime = NewLifetime();
            var step = 0;
            await client.Connect();

            client.Collector.Script(sample => {
                step++;
                sample.Gauge("a.first", step, "ms");

                if (step % 2 == 0)
                    sample.Gauge("b.gap", step * 10d, "count");

                sample.Gauge("c.third", step * 100d, "bytes");

                if (step > 20)
                    sample.Gauge("d.late", step * 1000d);
            });
            client.Telemetry.StartSession(lifetime, tag);

            await UniTask.WaitUntil(() => step > 40 && client.Transport.Sent.Count >= 5).Timeout(TimeSpan.FromSeconds(30));
            var id = client.Transport.StartAnswers.Single().SessionId;

            // Read while it is live, then closed: both are the same log.
            var live = await TelemetryTransportHost.Read($"sessions/{id}/samples?limit=1000");
            Assert.AreEqual("live", live.Value<string>("status"));
            Assert.IsNotEmpty(live["items"]);
            lifetime.Terminate();

            await TelemetryTransportHost.UntilClosed(tag, "ended");
            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);

            var withNames = client.Transport.Sent.Where(sent => sent.Batch.Names.Count > 0).ToArray();
            Assert.Less(withNames.Length, client.Transport.Sent.Count, "the batches after the first ones carry no names");
            CollectionAssert.AreEqual(new[] { "a.first", "c.third", "b.gap", "d.late" }, withNames.SelectMany(sent => sent.Batch.Names).Select(name => name.Name));

            // The relations the collector has set, on what the API gives.
            foreach (var sample in await TelemetryTransportHost.ReadAll(id.ToString(), "samples"))
            {
                var values = (JObject)sample["values"];
                var first = values["a.first"].Value<double>();

                Assert.AreEqual(first * 100d, values["c.third"].Value<double>());

                if (first % 2 == 0)
                    Assert.AreEqual(first * 10d, values["b.gap"].Value<double>());
                else
                    Assert.IsNull(values["b.gap"]);

                if (first > 20)
                    Assert.AreEqual(first * 1000d, values["d.late"].Value<double>());
                else
                    Assert.IsNull(values["d.late"]);
            }
        });
    }
}
