using System.Linq;
using NUnit.Framework;

namespace Global.Telemetry.Tests
{
    // The page before the build as spans: the navigation, the files before the build with their sizes, the gap to the build and the
    // spans of the template, on the time line of the page.
    public class TelemetryPageSpansTests
    {
        private static TelemetryPageTimeline Page()
        {
            return new TelemetryPageTimeline
            {
                now = 9000d,
                navigation = new TelemetryPageNavigation
                {
                    domainLookupStart = 10d,
                    domainLookupEnd = 40d,
                    connectStart = 40d,
                    connectEnd = 90d,
                    requestStart = 90d,
                    responseStart = 300d,
                    responseEnd = 320d,
                    domContentLoadedEventEnd = 2500d,
                    loadEventEnd = 2600d
                },
                files = new[]
                {
                    new TelemetryPageFile { name = "telegram-web-app.js", start = 330d, end = 2400d },
                    new TelemetryPageFile { name = "Build.wasm.unityweb", start = 2700d, end = 6000d, transfer = 9000000d, encoded = 8999000d, decoded = 30000000d },
                    new TelemetryPageFile { name = "Build.data.unityweb", start = 2700d, end = 5000d, transfer = 300d, encoded = 4000000d, decoded = 4000000d },
                    new TelemetryPageFile { name = "Build.framework.js", start = 2650d, end = 2690d, transfer = 0d, encoded = 1000d, decoded = 5000d },
                    new TelemetryPageFile { name = "music.bundle", start = 8000d, end = 8500d, transfer = 100d, encoded = 90d, decoded = 90d }
                },
                spans = new[]
                {
                    new TelemetryPageScriptSpan { name = "Unity loader", parent = -1, start = 2600d, end = 2700d },
                    new TelemetryPageScriptSpan { name = "Unity instance", parent = -1, start = 2700d, end = -1d },
                    new TelemetryPageScriptSpan { name = "Progress half", parent = 1, start = 4000d, end = 4000d }
                }
            };
        }

        private static TelemetryStartupSpan Named(TelemetryStartupSpan[] spans, string name)
        {
            return spans.Single(span => span.Name == name);
        }

        [Test]
        public void Build_TheRootCoversThePageUntilTheBuild_ParentsGoFirst()
        {
            var spans = TelemetryPageSpans.Build(Page(), 7000d).ToArray();

            Assert.AreEqual(TelemetryPageSpans.Root, spans[0].Name);
            Assert.AreEqual(-1, spans[0].ParentIndex);
            Assert.AreEqual(0d, spans[0].StartMs);
            Assert.AreEqual(7000d, spans[0].DurationMs);

            for (var i = 1; i < spans.Length; i++)
                Assert.Less(spans[i].ParentIndex, i, spans[i].Name);
        }

        [Test]
        public void Build_TheNavigation_HasItsStages_AndTheParsingOfTheDocument()
        {
            var spans = TelemetryPageSpans.Build(Page(), 7000d).ToArray();
            var navigation = Named(spans, "Navigation");

            Assert.AreEqual(320d, navigation.DurationMs);
            Assert.AreEqual(30d, Named(spans, "DNS").DurationMs);
            Assert.AreEqual(50d, Named(spans, "Connect").DurationMs);
            Assert.AreEqual(210d, Named(spans, "Request").DurationMs);
            Assert.AreEqual(20d, Named(spans, "Response").DurationMs);
            Assert.AreEqual(2180d, Named(spans, "Document").DurationMs);
            Assert.AreEqual(2280d, Named(spans, "Load event").DurationMs);
            Assert.IsFalse(spans.Any(span => span.Name == "Redirect"));
        }

        [Test]
        public void Build_TheFilesBeforeTheBuild_GoWithTheirNamesSizesAndCache()
        {
            var files = TelemetryPageSpans.Build(Page(), 7000d).Where(span => span.Name == "File").ToArray();

            CollectionAssert.AreEqual(
                new[] { "telegram-web-app.js", "Build.framework.js", "Build.wasm.unityweb", "Build.data.unityweb" },
                files.Select(file => file.File).ToArray());

            var wasm = files[2];
            Assert.AreEqual(2700d, wasm.StartMs);
            Assert.AreEqual(3300d, wasm.DurationMs);
            Assert.AreEqual(9000000L, wasm.TransferBytes);
            Assert.IsFalse(wasm.IsCached);
            Assert.IsTrue(files[1].IsCached, "nothing came for the body");
            Assert.IsTrue(files[3].IsCached, "only the headers of a revalidation came");
            Assert.IsFalse(files[0].IsCached, "another origin tells no sizes");
            Assert.AreEqual(0L, files[0].TransferBytes);
        }

        [Test]
        public void Build_TheGapFromTheLastFileToTheBuild_IsTheStartOfTheBuild()
        {
            var gap = Named(TelemetryPageSpans.Build(Page(), 7000d).ToArray(), "Build start");

            Assert.AreEqual(6000d, gap.StartMs);
            Assert.AreEqual(1000d, gap.DurationMs);
        }

        [Test]
        public void Build_TheSpansOfTheTemplate_KeepTheirNesting_AnOpenOneEndsAtTheBuild()
        {
            var spans = TelemetryPageSpans.Build(Page(), 7000d).ToArray();
            var instance = Named(spans, "Unity instance");
            var instanceIndex = System.Array.IndexOf(spans, instance);

            Assert.AreEqual(0, Named(spans, "Unity loader").ParentIndex);
            Assert.IsFalse(Named(spans, "Unity loader").IsUnfinished);
            Assert.AreEqual(4300d, instance.DurationMs);
            Assert.IsTrue(instance.IsUnfinished);
            Assert.AreEqual(instanceIndex, Named(spans, "Progress half").ParentIndex);
            Assert.AreEqual(0d, Named(spans, "Progress half").DurationMs);
        }

        [Test]
        public void Build_ASpanOfTheTemplateAfterTheBuild_StretchesTheRoot()
        {
            var page = Page();
            page.spans = new[] { new TelemetryPageScriptSpan { name = "Loading wheel", parent = -1, start = 100d, end = 12000d } };

            var spans = TelemetryPageSpans.Build(page, 7000d).ToArray();

            Assert.AreEqual(12000d, spans[0].DurationMs);
        }

        [TestCase("Build.wasm", true)]
        [TestCase("telegram-web-app.js", true)]
        [TestCase("", false)]
        [TestCase(".js", false)]
        [TestCase("a..js", false)]
        [TestCase("Build/Build.wasm", false)]
        [TestCase("a.js?v=1", false)]
        [TestCase("ivan@mail.js", false)]
        public void IsFileName_TheFormTheServerTakes(string name, bool expected)
        {
            Assert.AreEqual(expected, TelemetryPageSpans.IsFileName(name));
        }

        [Test]
        public void Build_AFileOutOfTheForm_StaysWithoutAName()
        {
            var page = Page();
            page.files = new[] { new TelemetryPageFile { name = "a b.js", start = 1d, end = 2d } };

            var file = TelemetryPageSpans.Build(page, 7000d).Single(span => span.Name == "File");

            Assert.IsNull(file.File);
        }

        [Test]
        public void Build_AnEmptyPage_IsTheRootAlone()
        {
            var spans = TelemetryPageSpans.Build(new TelemetryPageTimeline { now = 10d }, 5d);

            Assert.AreEqual(1, spans.Count);
            Assert.AreEqual(5d, spans[0].DurationMs);
        }
    }
}
