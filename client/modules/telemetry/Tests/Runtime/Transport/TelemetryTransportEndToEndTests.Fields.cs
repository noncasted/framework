using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests.Transport
{
    // The fields and their edges: what was sent is what is read.
    public partial class TelemetryTransportEndToEndTests
    {
        // One session with its data, closed in order: the id comes back for the reading.
        private async UniTask<Guid> Closed(TelemetryTransportClient client, string tag, Func<UniTask> during, int batches = 1)
        {
            var lifetime = NewLifetime();
            var sent = client.Transport.Sent.Count;
            var answers = client.Transport.StartAnswers.Count;

            client.Telemetry.StartSession(lifetime, tag);

            await UniTask.WaitUntil(() => client.Transport.StartAnswers.Count > answers).Timeout(TimeSpan.FromSeconds(15));
            Assert.AreEqual(TelemetrySendOutcome.Accepted, client.Transport.StartAnswers[answers].Outcome);

            await during();
            await WaitBatches(client, sent + batches);
            lifetime.Terminate();
            await TelemetryTransportHost.UntilClosed(tag, "ended");

            return client.Transport.StartAnswers[answers].SessionId;
        }

        [UnityTest]
        public IEnumerator Doubles_FiniteExact_NaNAndInfinityAbsent() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            await client.Connect();

            client.Collector.Script(sample => {
                sample.Gauge("v.pi", Math.PI, "ms");
                sample.Gauge("v.sum", 0.1d + 0.2d);
                sample.Gauge("v.negative", -1.5e-7d);
                sample.Gauge("v.zero", 0d);
                sample.Gauge("v.big", 1e300d, "bytes");
                sample.Gauge("v.tiny", double.Epsilon);
                sample.Gauge("v.int", 9007199254740993d);
                sample.Gauge("v.nan", double.NaN);
                sample.Gauge("v.inf", double.PositiveInfinity);
                sample.Gauge("v.neginf", double.NegativeInfinity);
                sample.Gauge("v.last", 1d);
            });

            var id = await Closed(client, Tag("doubles"), () => UniTask.Delay(300, DelayType.Realtime));

            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);

            var values = (JObject)(await TelemetryTransportHost.ReadAll(id.ToString(), "samples"))[0]["values"];
            Assert.AreEqual(Math.PI, values["v.pi"].Value<double>(), 0d);
            Assert.AreEqual(0.1d + 0.2d, values["v.sum"].Value<double>(), 0d);
            Assert.AreEqual(double.Epsilon, values["v.tiny"].Value<double>(), 0d);
            Assert.AreEqual(1d, values["v.last"].Value<double>(), 0d);
            Assert.IsNull(values["v.nan"]);
            Assert.IsNull(values["v.inf"]);
            Assert.IsNull(values["v.neginf"]);
        });

        // Near the limit of a double: an average of the summary must not overflow into a close that fails or a card that cannot be read.
        [UnityTest]
        public IEnumerator Doubles_LargestFinite_SessionClosesAndReads() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            await client.Connect();

            client.Collector.Script(sample => {
                sample.Gauge("v.max", double.MaxValue, "bytes");
                sample.Gauge("v.min", double.MinValue);
            });

            var id = await Closed(client, Tag("maxdouble"), () => UniTask.Delay(300, DelayType.Realtime));

            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);
            Assert.IsNotNull((await TelemetryTransportHost.Read("sessions/" + id))["digest"]["summary"]);
            Assert.IsNotNull(await TelemetryTransportHost.Read($"sessions/{id}/summary"));
        });

        [UnityTest]
        public IEnumerator Names_LimitsAndWrongOnes_ValuesStayOnTheirNames() => UniTask.ToCoroutine(async () =>
        {
            var client = Client(strictNames: false);
            var longName = new string('n', TelemetryLimits.MaxNameLength);
            var longUnit = new string('u', TelemetryLimits.MaxUnitLength);
            await client.Connect();

            client.Collector.Script(sample => {
                sample.Gauge("n.first", 1d, "ms");
                sample.Gauge("Wrong Name", 100d, "ms");
                sample.Gauge(longName + "x", 200d);
                sample.Gauge(longName, 2d, longUnit);
                sample.Gauge("n.bad_unit", 300d, "Not A Unit");
                sample.Gauge("n.long_unit", 400d, longUnit + "x");
                sample.Gauge("0.9_digits.ok", 3d);
                sample.Gauge("n.empty_unit", 500d, "");
                sample.Gauge("n.last", 4d);
            });

            var id = await Closed(client, Tag("names"), () => {
                client.Telemetry.Mark("Wrong Mark");
                client.Telemetry.Mark(longName);
                return UniTask.Delay(300, DelayType.Realtime);
            });

            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);

            var card = await TelemetryTransportHost.Read("sessions/" + id);
            var names = card["names"].OrderBy(name => name.Value<int>("slot")).ToArray();
            CollectionAssert.AreEqual(new[] { "n.first", longName, "0.9_digits.ok", "n.last" }, names.Select(name => name.Value<string>("name")));
            CollectionAssert.AreEqual(new[] { "ms", longUnit, null, null }, names.Select(name => name.Value<string>("unit")));

            var values = (JObject)(await TelemetryTransportHost.ReadAll(id.ToString(), "samples"))[0]["values"];
            Assert.AreEqual(1d, values["n.first"].Value<double>());
            Assert.AreEqual(2d, values[longName].Value<double>());
            Assert.AreEqual(3d, values["0.9_digits.ok"].Value<double>());
            Assert.AreEqual(4d, values["n.last"].Value<double>());
            Assert.AreEqual(longName, (await TelemetryTransportHost.ReadAll(id.ToString(), "marks")).Single().Value<string>("name"));
        });

        [UnityTest]
        public IEnumerator Device_EveryField_ReadAsSent() => UniTask.ToCoroutine(async () =>
        {
            var device = new TelemetryDevice("web", "1.2.3", "Chrome", 120, "Android", 14, "Adreno 650", 8, 0.25d, 1920, 1080, 2.625d);
            var client = Client(device);
            await client.Connect();

            var id = await Closed(client, Tag("device"), () => UniTask.Delay(200, DelayType.Realtime));

            CheckDevice(device, (await TelemetryTransportHost.Read("sessions/" + id))["device"]);
        });

        [UnityTest]
        public IEnumerator Device_NothingKnown_NullsNotZeros() => UniTask.ToCoroutine(async () =>
        {
            var device = new TelemetryDevice();
            var client = Client(device);
            await client.Connect();

            var id = await Closed(client, Tag("nodevice"), () => UniTask.Delay(200, DelayType.Realtime));

            CheckDevice(device, (await TelemetryTransportHost.Read("sessions/" + id))["device"]);
        });

        // What the editor itself reports: strings a real machine gives may not fit the rules of the server.
        [UnityTest]
        public IEnumerator Device_OfThisEditor_ReadAsSent() => UniTask.ToCoroutine(async () =>
        {
            var source = new UnityTelemetryDeviceSource();
            var client = Client(deviceSource: source);
            await client.Connect();

            var id = await Closed(client, Tag("realdevice"), () => UniTask.Delay(200, DelayType.Realtime));

            CheckDevice(source.Read(), (await TelemetryTransportHost.Read("sessions/" + id))["device"]);
        });

        private static void CheckDevice(TelemetryDevice sent, JToken read)
        {
            Assert.AreEqual(sent.Platform, read.Value<string>("platform"), "platform");
            Assert.AreEqual(sent.BuildVersion, read.Value<string>("buildVersion"), "buildVersion");
            Assert.AreEqual(sent.Browser, read.Value<string>("browser"), "browser");
            Assert.AreEqual(sent.BrowserMajor, read.Value<int?>("browserMajor"), "browserMajor");
            Assert.AreEqual(sent.Os, read.Value<string>("os"), "os");
            Assert.AreEqual(sent.OsMajor, read.Value<int?>("osMajor"), "osMajor");
            Assert.AreEqual(sent.Gpu, read.Value<string>("gpu"), "gpu");
            Assert.AreEqual(sent.Cores, read.Value<int?>("cores"), "cores");
            Assert.AreEqual(sent.MemoryClass, read.Value<double?>("memoryClass"), "memoryClass");
            Assert.AreEqual(sent.ScreenWidth, read.Value<int?>("screenWidth"), "screenWidth");
            Assert.AreEqual(sent.ScreenHeight, read.Value<int?>("screenHeight"), "screenHeight");
            Assert.AreEqual(sent.Dpr, read.Value<double?>("dpr"), "dpr");
        }

        [UnityTest]
        public IEnumerator ErrorLevels_ErrorExceptionAssertWarning_ReadAsTheirLevel() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            await client.Connect();

            var id = await Closed(client, Tag("levels"), async () => {
                Debug.LogError("E2E level error");
                Debug.LogException(new InvalidOperationException("E2E level exception"));
                Debug.LogAssertion("E2E level assertion");
                Debug.LogWarning("E2E level warning");
                Debug.Log("E2E level log");
                await UniTask.Delay(1500, DelayType.Realtime);
            });

            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);

            var errors = await TelemetryTransportHost.ReadAll(id.ToString(), "errors");
            Assert.AreEqual(4, errors.Count, "a plain log is not sent");
            Assert.AreEqual("error", errors.Single(error => error.Value<string>("message").Contains("level error")).Value<string>("level"));
            Assert.AreEqual("warning", errors.Single(error => error.Value<string>("message").Contains("level warning")).Value<string>("level"));
            Assert.AreEqual("exception", errors.Single(error => error.Value<string>("message").Contains("level exception")).Value<string>("level"));
            Assert.AreEqual("error", errors.Single(error => error.Value<string>("message").Contains("level assertion")).Value<string>("level"));

            var digest = (await TelemetryTransportHost.Read("sessions/" + id))["digest"];
            Assert.AreEqual(3, digest.Value<long>("errorCount"), "the warnings are counted apart");
            Assert.AreEqual(1, digest.Value<long>("warningCount"));

            var groups = digest["errorGroups"]["groups"];
            Assert.AreEqual("exception", groups.Single(group => group.Value<string>("message").Contains("level exception")).Value<string>("level"));
        });

        [UnityTest]
        public IEnumerator HiddenPage_SamplesMarked_OutOfTheSummary() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            var value = 10d;
            await client.Connect();

            client.Collector.Script(sample => sample.Gauge("h.value", value, "ms"));

            var id = await Closed(client, Tag("hidden"), async () => {
                await UniTask.Delay(250, DelayType.Realtime);
                value = 1000d;
                client.Page.Hide();
                await UniTask.Delay(250, DelayType.Realtime);
                value = 10d;
                client.Page.Show();
                await UniTask.Delay(250, DelayType.Realtime);
            });

            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);

            var samples = await TelemetryTransportHost.ReadAll(id.ToString(), "samples");
            var hidden = samples.Where(sample => sample.Value<bool>("hidden")).ToArray();
            Assert.IsNotEmpty(hidden);
            Assert.Less(hidden.Length, samples.Count);

            var summary = (await TelemetryTransportHost.Read("sessions/" + id))["digest"]["summary"];
            Assert.AreEqual(hidden.Length, summary.Value<int>("hiddenSamples"));
            Assert.AreEqual(samples.Count - hidden.Length, summary.Value<int>("samples"));
            Assert.AreEqual(10d, summary["metrics"].Single().Value<double>("max"), "a hidden sample stays out");
        });
    }
}
