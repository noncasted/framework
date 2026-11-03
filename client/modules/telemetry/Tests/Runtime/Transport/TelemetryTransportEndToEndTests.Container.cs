using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Internal;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests.Transport
{
    // The module as a game gets it: the generated container, the standard collectors, the device of this editor, the profiler. What
    // a real machine reports must fit the rules of the server: nothing rejected, every name in the API.
    public partial class TelemetryTransportEndToEndTests
    {
        [UnityTest]
        public IEnumerator Container_StandardCollectorsAndProfiler_ReadOnTheServer() => UniTask.ToCoroutine(async () =>
        {
            var startupProperty = typeof(GameProfiler).GetProperty(nameof(GameProfiler.Startup), BindingFlags.Public | BindingFlags.Static);
            var traces = ProfilerTraceStorage.List().ToArray();
            var client = Client();
            var updater = new GameObject("updater").AddComponent<Updater>();
            var tag = Tag("container");
            await client.Connect();

            startupProperty.SetValue(null, null);
            var root = GameProfiler.Begin("Startup");
            GameProfiler.Scope("Load https://api.example.com/v1?token=abc123").Dispose();

            try
            {
                TelemetryTransportRoots.Use(client, updater, tag);
                var scope = await new ServiceScopeLoader().Load(new ScopeLoadOptions(null, TelemetryTransportRoots.Game));
                await scope.Initialize();

                var row = await TelemetryTransportHost.Until(
                    async () => {
                        // The row of a live session in the list has no names yet: the card is read from its grain.
                        var listed = await TelemetryTransportHost.Session(tag);
                        return listed == null ? null : await TelemetryTransportHost.Read("sessions/" + listed.Value<string>("id"));
                    },
                    read => read.Value<int>("ackSeq") >= 3 && read["names"].Any(name => name.Value<string>("name") == "memory.mono.used"),
                    "the standard metrics on the server");
                var id = row.Value<string>("id");

                Debug.LogError("E2E broken in a real scope");
                await UniTask.Delay(1500, DelayType.Realtime);

                var startupId = row.Value<string>("startupId");
                Assert.IsNotNull(startupId, "the session refers to the trace of the profiler");

                var snapshot = await TelemetryTransportHost.Read("startups/" + startupId);
                Assert.IsFalse(snapshot["startup"].Value<bool>("final"));
                Assert.AreEqual(0, snapshot["startup"].Value<int>("rejectedSpans"));
                Assert.AreEqual(0, snapshot["startup"].Value<int>("cutNames"));
                StringAssert.DoesNotContain("abc123", snapshot.ToString());

                GameProfiler.Finish();
                await TelemetryTransportHost.Until(
                    () => TelemetryTransportHost.Read("startups/" + startupId),
                    read => read["startup"].Value<bool>("final"),
                    "the final trace of the profiler");

                await scope.Dispose();

                var closed = await TelemetryTransportHost.UntilClosed(tag, "ended");
                var card = await TelemetryTransportHost.Read("sessions/" + id);

                Assert.AreEqual(0, card["digest"].Value<int>("rejectedRequests"), "the rows of a real client fit the server");
                Assert.AreEqual(1, card["digest"].Value<int>("marks"));
                Assert.AreEqual("E2E broken in a real scope", card["digest"]["errorGroups"]["groups"].Single().Value<string>("message"));
                Assert.AreEqual(Application.platform.ToString().Length > 0, closed["device"].Value<string>("platform").Length > 0);
                Assert.IsTrue(card["names"].Any(name => name.Value<string>("name") == "frame.fps" || name.Value<string>("name").StartsWith("frame.")), "the frames of the PlayMode");
                Assert.Greater(card["digest"]["summary"].Value<int>("samples"), 0);
            }
            finally
            {
                UnityEngine.Object.Destroy(updater.gameObject);
                startupProperty.SetValue(null, null);

                foreach (var path in ProfilerTraceStorage.List().Except(traces))
                    File.Delete(path);

                if (Directory.Exists(ProfilerTraceStorage.Directory) && Directory.GetFileSystemEntries(ProfilerTraceStorage.Directory).Length == 0)
                    Directory.Delete(ProfilerTraceStorage.Directory);
            }
        });
    }
}
