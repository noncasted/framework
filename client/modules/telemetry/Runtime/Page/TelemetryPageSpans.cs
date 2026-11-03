using System;
using System.Collections.Generic;
using System.Linq;

namespace Global.Telemetry
{
    // The page before the build as spans of the startup trace, on the time line of the page (0 is performance.timeOrigin), depth
    // first. The root Page holds the navigation of the document, its parsing and load event, the files loaded before the build
    // started, the gap from the last of them to the start of the build (the compilation of the wasm, the start of the instance) and
    // the spans of the template. A span of the template still open ends at the start of the build and is unfinished.
    public static class TelemetryPageSpans
    {
        public const string Root = "Page";
        public const int MaxFiles = 64;

        public static IReadOnlyList<TelemetryStartupSpan> Build(TelemetryPageTimeline page, double buildStartMs)
        {
            var spans = new List<TelemetryStartupSpan> { default };
            var end = buildStartMs;

            end = Math.Max(end, AddNavigation(spans, page.navigation));
            end = Math.Max(end, AddFiles(spans, page.files, buildStartMs));
            end = Math.Max(end, AddScriptSpans(spans, page.spans, buildStartMs));

            spans[0] = new TelemetryStartupSpan(-1, Root, 0d, end, 0, false, 0L);

            return spans;
        }

        // The last segment of an address as the server takes it: words of [A-Za-z0-9_-] between dots.
        public static bool IsFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > TelemetryLimits.MaxStartupFileLength)
                return false;

            if (name[0] == '.' || name[name.Length - 1] == '.' || name.Contains(".."))
                return false;

            return name.All(symbol => symbol is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-');
        }

        // Cached: nothing came over the network for a body (transfer 0), or only the headers of a revalidation (304).
        public static bool IsCached(TelemetryPageFile file)
        {
            return (file.transfer == 0d && file.decoded > 0d) || (file.transfer > 0d && file.transfer < file.encoded);
        }

        private static double AddNavigation(List<TelemetryStartupSpan> spans, TelemetryPageNavigation navigation)
        {
            if (navigation == null || navigation.responseEnd <= 0d)
                return 0d;

            var root = spans.Count;
            spans.Add(Span(0, "Navigation", 0d, navigation.responseEnd));

            AddStage(spans, root, "Redirect", navigation.redirectStart, navigation.redirectEnd);
            AddStage(spans, root, "DNS", navigation.domainLookupStart, navigation.domainLookupEnd);
            AddStage(spans, root, "Connect", navigation.connectStart, navigation.connectEnd);
            AddStage(spans, root, "Request", navigation.requestStart, navigation.responseStart);
            AddStage(spans, root, "Response", navigation.responseStart, navigation.responseEnd);

            // The parsing runs the scripts of the head: a blocking one of another origin (an SDK of a platform) lands here.
            AddStage(spans, 0, "Document", navigation.responseEnd, navigation.domContentLoadedEventEnd);
            AddStage(spans, 0, "Load event", navigation.responseEnd, navigation.loadEventEnd);

            return Math.Max(navigation.responseEnd, Math.Max(navigation.domContentLoadedEventEnd, navigation.loadEventEnd));
        }

        private static void AddStage(List<TelemetryStartupSpan> spans, int parent, string name, double start, double end)
        {
            if (start > 0d && end > start)
                spans.Add(Span(parent, name, start, end));
        }

        private static double AddFiles(List<TelemetryStartupSpan> spans, TelemetryPageFile[] files, double buildStartMs)
        {
            var before = (files ?? Array.Empty<TelemetryPageFile>())
                         .Where(file => file != null && file.start < buildStartMs && file.end >= file.start)
                         .OrderBy(file => file.start)
                         .Take(MaxFiles)
                         .ToArray();

            if (before.Length == 0)
                return 0d;

            var first = before.Min(file => file.start);
            var last = before.Max(file => file.end);
            var root = spans.Count;

            spans.Add(Span(0, "Files", first, last));

            foreach (var file in before)
            {
                spans.Add(new TelemetryStartupSpan(
                    root,
                    "File",
                    file.start,
                    file.end - file.start,
                    0,
                    false,
                    0L,
                    IsFileName(file.name) ? file.name : null,
                    (long)file.transfer,
                    IsCached(file)));
            }

            if (buildStartMs > last)
                spans.Add(Span(0, "Build start", last, buildStartMs));

            return last;
        }

        private static double AddScriptSpans(List<TelemetryStartupSpan> spans, TelemetryPageScriptSpan[] script, double buildStartMs)
        {
            script ??= Array.Empty<TelemetryPageScriptSpan>();

            var indices = new int[script.Length];
            var end = 0d;

            for (var i = 0; i < script.Length; i++)
            {
                var span = script[i];
                indices[i] = -1;

                if (span == null || string.IsNullOrWhiteSpace(span.name) || span.start < 0d)
                    continue;

                var parent = span.parent >= 0 && span.parent < i && indices[span.parent] >= 0 ? indices[span.parent] : 0;
                var isOpen = span.end < span.start;
                var spanEnd = isOpen ? Math.Max(buildStartMs, span.start) : span.end;

                indices[i] = spans.Count;
                spans.Add(new TelemetryStartupSpan(parent, span.name, span.start, spanEnd - span.start, 0, isOpen, 0L));
                end = Math.Max(end, spanEnd);
            }

            return end;
        }

        private static TelemetryStartupSpan Span(int parent, string name, double start, double end)
        {
            return new TelemetryStartupSpan(parent, name, start, end - start, 0, false, 0L);
        }
    }
}
