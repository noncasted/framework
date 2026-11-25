using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests.Transport
{
    // The free texts: scrubbed on the client, scrubbed again on the server, cut to the limits.
    public partial class TelemetryTransportEndToEndTests
    {
        private const string _personal = "E2E failed for ivan.petrov@example.com at https://api.example.com/v1/user?token=abc123 from 192.168.17.34 id 3f2504e0-4f89-11d3-9a0c-0305e82c3301";
        private const string _personalStack = "at Game.Save () in /home/nndd/secret/Save.cs:12\nat Game.Mail (ivan.petrov@example.com)";

        private static readonly string[] _leaks =
        {
            "ivan.petrov", "example.com", "abc123", "192.168.17.34", "3f2504e0", "/home/nndd"
        };

        // The whole way: the log, the queue, the scrubber of the client, the wire, the scrubber of the server.
        [UnityTest]
        public IEnumerator Scrubbing_ClientPath_NoPersonalDataAndReadAsSent() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            await client.Connect();

            var id = await Closed(client, Tag("scrub"), async () => {
                client.ErrorQueue.Add(TelemetryErrorLevel.Error, _personal, _personalStack);
                await UniTask.WaitUntil(() => client.Transport.Sent.Any(sent => sent.Batch.Errors.Count > 0)).Timeout(TimeSpan.FromSeconds(15));
            });

            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);
            await CheckNoLeaks(id);

            var sent = client.Transport.Sent.SelectMany(item => item.Batch.Errors).Single();
            Assert.AreEqual(TelemetryScrubber.Scrub(_personal), sent.Message);
        });

        // The server on its own: past the scrubber of the client (ITelemetryErrors takes clean text), the personal data reaches
        // the wire and only the server is left to catch it.
        [UnityTest]
        public IEnumerator Scrubbing_ServerAlone_NoPersonalData() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            await client.Connect();

            var id = await Closed(client, Tag("scrubserver"), async () => {
                client.Errors.Add(TelemetryErrorLevel.Exception, null, _personal, _personalStack, 3);
                await UniTask.WaitUntil(() => client.Transport.Sent.Any(sent => sent.Batch.Errors.Count > 0)).Timeout(TimeSpan.FromSeconds(15));
            });

            await CheckNoLeaks(id);

            var error = (await TelemetryTransportHost.ReadAll(id.ToString(), "errors")).Single();
            Assert.AreEqual("exception", error.Value<string>("level"));
            Assert.AreEqual(3, error.Value<int>("repeat"));
            StringAssert.Contains("E2E failed", error.Value<string>("message"));
        });

        private static async UniTask CheckNoLeaks(Guid id)
        {
            var errors = (await TelemetryTransportHost.Read($"sessions/{id}/errors?limit=100")).ToString();
            var card = (await TelemetryTransportHost.Read($"sessions/{id}")).ToString();

            foreach (var leak in _leaks)
            {
                StringAssert.DoesNotContain(leak, errors);
                StringAssert.DoesNotContain(leak, card);
            }
        }

        [UnityTest]
        public IEnumerator Texts_OverTheLimits_CutAndReadAsSent() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            await client.Connect();

            var id = await Closed(client, Tag("cuts"), async () => {
                client.ErrorQueue.Add(TelemetryErrorLevel.Error, "E2E " + Words(TelemetryLimits.MaxMessageLength * 2), Words(TelemetryLimits.MaxStackLength * 2));
                await UniTask.WaitUntil(() => client.Transport.Sent.Any(sent => sent.Batch.Errors.Count > 0)).Timeout(TimeSpan.FromSeconds(15));
            });

            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);

            var error = (await TelemetryTransportHost.ReadAll(id.ToString(), "errors")).Single();
            Assert.AreEqual(TelemetryLimits.MaxMessageLength, error.Value<string>("message").Length);
            Assert.AreEqual(TelemetryLimits.MaxStackLength, error.Value<string>("stack").Length);
        });

        // A cut that falls between the two halves of an emoji, and text out of ASCII: the session still closes and reads.
        [UnityTest]
        public IEnumerator Texts_CutThroughASurrogatePair_SessionClosesAndReads() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            await client.Connect();

            var message = "E2E Ошибка " + Words(TelemetryLimits.MaxMessageLength - 12) + "\U0001F600\U0001F600 tail";
            var stack = Words(TelemetryLimits.MaxStackLength - 1) + "\U0001F600 tail";

            var id = await Closed(client, Tag("surrogate"), async () => {
                client.ErrorQueue.Add(TelemetryErrorLevel.Error, message, stack);
                await UniTask.WaitUntil(() => client.Transport.Sent.Any(sent => sent.Batch.Errors.Count > 0)).Timeout(TimeSpan.FromSeconds(15));
            });

            var error = (await TelemetryTransportHost.ReadAll(id.ToString(), "errors")).Single();
            StringAssert.StartsWith("E2E ", error.Value<string>("message"));
            StringAssert.DoesNotContain("\uFFFD", error.Value<string>("message"));
            await TelemetryReadCheck.ReadEqualsSent(client.Transport, id);
        });

        // Past the cleaning of the client, half of a pair on the wire: the server still closes the session and gives it back.
        [UnityTest]
        public IEnumerator Texts_LoneSurrogateOnTheWire_SessionClosesAndReads() => UniTask.ToCoroutine(async () =>
        {
            var client = Client();
            await client.Connect();

            var id = await Closed(client, Tag("lone"), async () => {
                client.Errors.Add(TelemetryErrorLevel.Error, null, "E2E half \uD83D", "at half \uD83D", 1);
                await UniTask.WaitUntil(() => client.Transport.Sent.Any(sent => sent.Batch.Errors.Count > 0)).Timeout(TimeSpan.FromSeconds(15));
            });

            StringAssert.StartsWith("E2E half", (await TelemetryTransportHost.ReadAll(id.ToString(), "errors")).Single().Value<string>("message"));
        });

        private static string Words(int length)
        {
            var text = "";

            while (text.Length < length)
                text += "word ";

            return text.Substring(0, length);
        }
    }
}
