using System;
using NUnit.Framework;
using UnityEngine;

namespace Global.Telemetry.Tests
{
    // The HTTP JSON written by JsonUtility: valid JSON (no NaN), absent values said by flags, read back to the same values.
    public class TelemetryHttpMessagesTests
    {
        private static readonly Guid _link = new Guid(7, 0, 0, new byte[8]);
        private static readonly Guid _session = new Guid(8, 0, 0, new byte[8]);

        private static T RoundTrip<T>(T message)
        {
            return JsonUtility.FromJson<T>(JsonUtility.ToJson(message));
        }

        [Test]
        public void Sample_NaNAndInfinity_InEmptyNotInTheJson()
        {
            var sample = new TelemetryBatchSample(1200, new[] { 1.5d, double.NaN, double.PositiveInfinity, 3d }, true);
            var batch = new TelemetryBatch(Array.Empty<TelemetryBatchName>(), new[] { sample }, Array.Empty<TelemetryBatchMark>(),
                Array.Empty<TelemetryBatchError>());

            var json = JsonUtility.ToJson(TelemetryHttpMessages.ToBatch(_link, _session, batch));
            var read = JsonUtility.FromJson<TelemetryHttpBatch>(json).samples[0];

            StringAssert.DoesNotContain("NaN", json);
            StringAssert.DoesNotContain("Infinity", json);
            Assert.AreEqual(1200, read.time);
            CollectionAssert.AreEqual(new[] { 1.5d, 0d, 0d, 3d }, read.values);
            CollectionAssert.AreEqual(new[] { 1, 2 }, read.empty);
            Assert.IsTrue(read.hidden);
        }

        [Test]
        public void Batch_GuidsAsText_TheRowsKept()
        {
            var batch = new TelemetryBatch(
                new[] { new TelemetryBatchName("frame.dt", "ms"), new TelemetryBatchName("count", null) },
                Array.Empty<TelemetryBatchSample>(),
                new[] { new TelemetryBatchMark(10, "load.end", "menu", 250), new TelemetryBatchMark(20, "page.hidden") },
                new[] { new TelemetryBatchError(30, TelemetryErrorLevel.Warning, "w", "at A", 2) });

            var json = JsonUtility.ToJson(TelemetryHttpMessages.ToBatch(_link, _session, batch));
            var read = JsonUtility.FromJson<TelemetryHttpBatch>(json);

            StringAssert.Contains($"\"linkId\":\"{_link}\"", json);
            Assert.AreEqual(_session, Guid.Parse(read.sessionId));
            Assert.AreEqual("ms", read.names[0].unit);
            Assert.AreEqual("", read.names[1].unit);
            Assert.IsTrue(read.marks[0].hasDuration);
            Assert.AreEqual(250, read.marks[0].durationMs);
            Assert.AreEqual("menu", read.marks[0].detail);
            Assert.IsFalse(read.marks[1].hasDuration);
            Assert.AreEqual("", read.marks[1].detail);
            Assert.AreEqual("Warning", read.errors[0].level);
            Assert.AreEqual(2, read.errors[0].repeat);
        }

        [Test]
        public void SessionStart_NoPrevious_FlagOff()
        {
            var start = new TelemetrySessionStart(new[] { "menu" }, new TelemetryDevice(platform: "WebGL", cores: 4, dpr: 2), Guid.Empty);

            var read = RoundTrip(TelemetryHttpMessages.ToSessionStart(_link, start));

            Assert.IsFalse(read.hasPrevious);
            Assert.AreEqual(Guid.Empty, Guid.Parse(read.previous.sessionId));
            Assert.AreEqual(Guid.Empty, Guid.Parse(read.startupId));
            CollectionAssert.AreEqual(new[] { "menu" }, read.tags);
            Assert.AreEqual("WebGL", read.device.platform);
            Assert.AreEqual(4, read.device.cores);
            Assert.AreEqual(0, read.device.osMajor);
            Assert.AreEqual(2d, read.device.dpr);
        }

        [Test]
        public void SessionStart_Previous_Kept()
        {
            var previous = new TelemetryPreviousSession(_session, new[] { "run" }, 5000, 120, new[] { new TelemetryBatchMark(4000, "load.start", "level") },
                new[] { "frame.dt.max" }, new[] { new TelemetryBatchSample(4900, new[] { double.NaN }, false) });
            var start = new TelemetrySessionStart(new[] { "menu" }, new TelemetryDevice(), Guid.Empty, previous);

            var read = RoundTrip(TelemetryHttpMessages.ToSessionStart(_link, start));

            Assert.IsTrue(read.hasPrevious);
            Assert.AreEqual(_session, Guid.Parse(read.previous.sessionId));
            Assert.AreEqual(5000, read.previous.lastSampleTime);
            Assert.AreEqual(120, read.previous.sinceMs);
            Assert.AreEqual("level", read.previous.marks[0].detail);
            CollectionAssert.AreEqual(new[] { "frame.dt.max" }, read.previous.sampleNames);
            CollectionAssert.AreEqual(new[] { 0 }, read.previous.samples[0].empty);
        }

        [Test]
        public void StartupTrace_SpansKept()
        {
            var spans = new[]
            {
                new TelemetryStartupSpan(-1, "Page", 0, 900.5, 0, false, 0, null, 0, false),
                new TelemetryStartupSpan(0, "File", 10, 300, 0, false, 0, "build.wasm", 4096, true)
            };
            var trace = new TelemetryStartupTrace(_session, new TelemetryDevice(), "Startup", true, 1500.25, 40, spans);

            var read = RoundTrip(TelemetryHttpMessages.ToStartupTrace(_link, trace));

            Assert.AreEqual(_session, Guid.Parse(read.startupId));
            Assert.IsTrue(read.final);
            Assert.AreEqual(1500.25, read.durationMs);
            Assert.AreEqual(40, read.frames);
            Assert.AreEqual(-1, read.spans[0].parentIndex);
            Assert.AreEqual("", read.spans[0].file);
            Assert.AreEqual(900.5, read.spans[0].durationMs);
            Assert.AreEqual("build.wasm", read.spans[1].file);
            Assert.AreEqual(4096, read.spans[1].transferBytes);
            Assert.IsTrue(read.spans[1].cached);
        }
    }
}
