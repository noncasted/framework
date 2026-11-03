using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests.Transport
{
    // What the client has put on the wire against what the API gives back, field by field. The values go by the slots of the names on
    // the client and by the names in the API: a value that moved to another slot shows here.
    public static class TelemetryReadCheck
    {
        public static async UniTask ReadEqualsSent(RecordingSocketTransport transport, Guid session)
        {
            var batches = transport.Sent.Where(sent => sent.Session == session).ToArray();
            var id = session.ToString();
            var row = await TelemetryTransportHost.Read("sessions/" + id);

            Assert.AreEqual(0, row["digest"]?.Type == JTokenType.Null || row["digest"] == null ? 0 : row["digest"].Value<int>("rejectedRequests"), "rejected by the server");

            var names = batches.SelectMany(sent => sent.Batch.Names).ToArray();
            var read = row["names"].OrderBy(name => name.Value<int>("slot")).ToArray();

            CollectionAssert.AreEqual(names.Select(name => name.Name), read.Select(name => name.Value<string>("name")), "names");
            CollectionAssert.AreEqual(names.Select(name => name.Unit), read.Select(name => name.Value<string>("unit")), "units");

            CheckSamples(batches.SelectMany(sent => sent.Batch.Samples).ToArray(), await TelemetryTransportHost.ReadAll(id, "samples"), names);
            CheckErrors(batches.SelectMany(sent => sent.Batch.Errors).ToArray(), await TelemetryTransportHost.ReadAll(id, "errors"));
            CheckMarks(batches.SelectMany(sent => sent.Batch.Marks).ToArray(), await TelemetryTransportHost.ReadAll(id, "marks"));
        }

        private static void CheckSamples(IReadOnlyList<TelemetryBatchSample> sent, JArray read, IReadOnlyList<TelemetryBatchName> names)
        {
            Assert.AreEqual(sent.Count, read.Count, "samples");

            for (var i = 0; i < sent.Count; i++)
            {
                Assert.AreEqual(sent[i].Time, read[i].Value<long>("time"), $"time of the sample {i}");
                Assert.AreEqual(sent[i].IsHidden, read[i].Value<bool>("hidden"), $"hidden of the sample {i}");

                var values = (JObject)read[i]["values"];
                var expected = sent[i].Values.Select((value, slot) => (value, slot)).Where(pair => double.IsNaN(pair.value)).Select(pair => names[pair.slot].Name);

                foreach (var name in expected)
                    Assert.IsNull(values[name], $"no value of {name} in the sample {i}");

                var count = 0;

                for (var slot = 0; slot < sent[i].Values.Count; slot++)
                {
                    if (double.IsNaN(sent[i].Values[slot]))
                        continue;

                    count++;
                    Assert.AreEqual(sent[i].Values[slot], values[names[slot].Name].Value<double>(), 0d, $"{names[slot].Name} in the sample {i}");
                }

                Assert.AreEqual(count, values.Count, $"the values of the sample {i}");
            }
        }

        private static void CheckErrors(IReadOnlyList<TelemetryBatchError> sent, JArray read)
        {
            Assert.AreEqual(sent.Count, read.Count, "errors");

            for (var i = 0; i < sent.Count; i++)
            {
                Assert.AreEqual(sent[i].Time, read[i].Value<long>("time"), $"time of the error {i}");
                Assert.AreEqual(sent[i].Level.ToString().ToLowerInvariant(), read[i].Value<string>("level"), $"level of the error {i}");
                Assert.AreEqual(sent[i].Message, read[i].Value<string>("message"), $"message of the error {i}");
                Assert.AreEqual(sent[i].Stack, read[i].Value<string>("stack"), $"stack of the error {i}");
                Assert.AreEqual(sent[i].Repeat, read[i].Value<int>("repeat"), $"repeat of the error {i}");
            }
        }

        private static void CheckMarks(IReadOnlyList<TelemetryBatchMark> sent, JArray read)
        {
            Assert.AreEqual(sent.Count, read.Count, "marks");

            for (var i = 0; i < sent.Count; i++)
            {
                Assert.AreEqual(sent[i].Time, read[i].Value<long>("time"), $"time of the mark {i}");
                Assert.AreEqual(sent[i].Name, read[i].Value<string>("name"), $"name of the mark {i}");
            }
        }
    }
}
