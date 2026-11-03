using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Telemetry.Tests
{
    // A trace of the profiler as it goes to the server: the hierarchy, the cleaning of the names and the limits.
    public class TelemetryStartupTracesTests
    {
        private static readonly Guid _id = new Guid(3, 0, 0, new byte[8]);
        private static readonly TelemetryDevice _device = new TelemetryDevice(cores: 2);

        private static ProfilerSpanData Span(int id, int parent, string name)
        {
            return new ProfilerSpanData { Id = id, ParentId = parent, Name = name };
        }

        private static TelemetryStartupTrace Build(params ProfilerSpanData[] spans)
        {
            return Build(FakeTelemetryStartupSource.Trace("Trace", spans));
        }

        // With no budget to run out of, it ends at once.
        private static TelemetryStartupTrace Build(ProfilerTraceData data, bool isFinal = true)
        {
            return TelemetryStartupTraces.Build(_id, _device, data, isFinal, double.MaxValue, TelemetryStartupPrefix.None).GetAwaiter().GetResult();
        }

        private static string PlainName(int length)
        {
            return string.Concat(Enumerable.Repeat("word ", length)).Substring(0, length);
        }

        private static int ServerLimit(string name)
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../../../backend/ClientTelemetry/Contract/ClientTelemetryLimits.cs"));
            var match = Regex.Match(File.ReadAllText(path), $@"const int {name} = ([0-9 *]+);");

            Assert.IsTrue(match.Success, name);

            return match.Groups[1].Value.Split('*').Aggregate(1, (value, part) => value * int.Parse(part.Trim()));
        }

        [Test]
        public void Limits_TheSameAsOfTheServer()
        {
            Assert.AreEqual(ServerLimit(nameof(TelemetryLimits.MaxStartupSpans)), TelemetryLimits.MaxStartupSpans);
            Assert.AreEqual(ServerLimit(nameof(TelemetryLimits.MaxStartupNameLength)), TelemetryLimits.MaxStartupNameLength);
            Assert.AreEqual(ServerLimit(nameof(TelemetryLimits.MaxStartupBytes)), TelemetryLimits.MaxStartupBytes);
        }

        [Test]
        public void Fields_AllPassed_NoClockOfTheDevice()
        {
            var data = FakeTelemetryStartupSource.Trace("Trace", new ProfilerSpanData
            {
                Id = 0,
                ParentId = -1,
                Name = "Root",
                StartMs = 1.5d,
                DurationMs = 2.5d,
                Frames = 3,
                Unfinished = true,
                AllocBytes = 7L
            });

            var trace = Build(data, false);
            var span = trace.Spans.Single();

            Assert.AreEqual(_id, trace.StartupId);
            Assert.AreSame(_device, trace.Device);
            Assert.AreEqual("Trace", trace.Name);
            Assert.IsFalse(trace.IsFinal);
            Assert.AreEqual(100d, trace.DurationMs);
            Assert.AreEqual(5, trace.Frames);
            Assert.AreEqual(-1, span.ParentIndex);
            Assert.AreEqual("Root", span.Name);
            Assert.AreEqual(1.5d, span.StartMs);
            Assert.AreEqual(2.5d, span.DurationMs);
            Assert.AreEqual(3, span.Frames);
            Assert.IsTrue(span.IsUnfinished);
            Assert.AreEqual(7L, span.AllocBytes);
        }

        [Test]
        public void Hierarchy_ParentIndexBelowOwnIndex()
        {
            var trace = Build(Span(0, -1, "a"), Span(1, 0, "b"), Span(2, 1, "c"), Span(3, 0, "d"), Span(4, -1, "e"));

            CollectionAssert.AreEqual(new[] { -1, 0, 1, 0, -1 }, trace.Spans.Select(span => span.ParentIndex).ToArray());
        }

        [Test]
        public void Names_GoThroughTheScrubber()
        {
            var trace = Build(Span(0, -1, "GET https://api.game.example/users/123?token=abc"));

            Assert.AreEqual("GET <url>", trace.Spans[0].Name);
        }

        [Test]
        public void Names_CutToTheLimit()
        {
            var trace = Build(FakeTelemetryStartupSource.Trace(PlainName(300), Span(0, -1, PlainName(300))));

            Assert.AreEqual(TelemetryLimits.MaxStartupNameLength, trace.Name.Length);
            Assert.AreEqual(TelemetryLimits.MaxStartupNameLength, trace.Spans[0].Name.Length);
        }

        [Test]
        public void Spans_OverTheLimit_TailCut_ParentsStay()
        {
            var spans = Enumerable.Range(0, TelemetryLimits.MaxStartupSpans + 10)
                                  .Select(index => Span(index, index - 1, "s"))
                                  .ToArray();

            var trace = Build(spans);

            Assert.AreEqual(TelemetryLimits.MaxStartupSpans, trace.Spans.Count);
            Assert.AreEqual(trace.Spans.Count - 2, trace.Spans[trace.Spans.Count - 1].ParentIndex);
        }

        [UnityTest]
        public IEnumerator Budget_OverIt_TheRestOnTheNextFrames_TheSameTrace() => UniTask.ToCoroutine(async () =>
        {
            var data = FakeTelemetryStartupSource.Trace("Trace", Span(0, -1, "a"), Span(1, 0, "b"), Span(2, 0, "c"));
            var building = TelemetryStartupTraces.Build(_id, _device, data, true, 0d, TelemetryStartupPrefix.None);

            Assert.AreEqual(UniTaskStatus.Pending, building.Status);

            var trace = await building;

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, trace.Spans.Select(span => span.Name).ToArray());
        });

        [Test]
        public void Bytes_OverTheEstimateOfTheServer_TailCut()
        {
            var name = PlainName(TelemetryLimits.MaxStartupNameLength);
            var spans = Enumerable.Range(0, TelemetryLimits.MaxStartupSpans)
                                  .Select(index => Span(index, -1, name))
                                  .ToArray();

            var trace = Build(spans);
            var bytes = trace.Spans.Sum(span => TelemetryLimits.StartupSpanBytes + span.Name.Length);

            Assert.Less(trace.Spans.Count, spans.Length);
            Assert.LessOrEqual(bytes, TelemetryLimits.MaxStartupBytes);
            Assert.Greater(bytes + TelemetryLimits.StartupSpanBytes + name.Length, TelemetryLimits.MaxStartupBytes);
        }

        [Test]
        public void Page_GoesFirst_TheSpansOfTheBuildMoveByItsStart()
        {
            var page = new TelemetryStartupPrefix(
                new[]
                {
                    new TelemetryStartupSpan(-1, "Page", 0d, 4000d, 0, false, 0L),
                    new TelemetryStartupSpan(0, "File", 10d, 3000d, 0, false, 0L, "Build.wasm", 900L, true)
                },
                4000d);
            var data = FakeTelemetryStartupSource.Trace("Trace", Span(0, -1, "Startup"), Span(1, 0, "Scope: Global"));
            data.Spans[1].StartMs = 5d;

            var trace = TelemetryStartupTraces.Build(_id, _device, data, true, double.MaxValue, page).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "Page", "File", "Startup", "Scope: Global" }, trace.Spans.Select(span => span.Name).ToArray());
            CollectionAssert.AreEqual(new[] { -1, 0, -1, 2 }, trace.Spans.Select(span => span.ParentIndex).ToArray());
            Assert.AreEqual(4000d, trace.Spans[2].StartMs);
            Assert.AreEqual(4005d, trace.Spans[3].StartMs);
            Assert.AreEqual("Build.wasm", trace.Spans[1].File);
            Assert.AreEqual(900L, trace.Spans[1].TransferBytes);
            Assert.IsTrue(trace.Spans[1].IsCached);
            Assert.AreEqual(4100d, trace.DurationMs, "the trace counts from the origin of the page");
        }

        [Test]
        public void Page_TheBuildStartsWhereTheClocksMeet()
        {
            var fixture = new TelemetryFixture();
            fixture.PageTimeline.Set(new TelemetryPageTimeline { now = 9000d });
            fixture.StartupSource.SetSinceStart(2500d);

            var prefix = new TelemetryStartupPage(fixture.PageTimeline, fixture.StartupSource).Read();

            Assert.AreEqual(6500d, prefix.OffsetMs);
            Assert.AreEqual("Page", prefix.Spans[0].Name);
            Assert.AreEqual(6500d, prefix.Spans[0].DurationMs);
        }

        [Test]
        public void Page_NoneOutsideTheWeb()
        {
            var fixture = new TelemetryFixture();

            var prefix = new TelemetryStartupPage(fixture.PageTimeline, fixture.StartupSource).Read();

            Assert.AreEqual(0, prefix.Spans.Count);
            Assert.AreEqual(0d, prefix.OffsetMs);
        }
    }
}
