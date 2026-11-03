using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry
{
    // A trace of the profiler as the module sends it: within the limits of the server, the names cleaned, the time only from the
    // start of the trace. Its spans go depth first and a span id is its index, so a tail cut leaves every parent in place. On the
    // web the spans of the page go first (TelemetryPageSpans) and the trace starts at the origin of the page: the spans of the build
    // move by the start of the build on that time line.
    //
    // The cleaning of a start of a few hundred names takes frames (about 0.3 ms a name in the editor, 40 expressions each), and the
    // final trace closes when the game shows itself: over the budget of a frame the rest goes on the next one.
    public static class TelemetryStartupTraces
    {
        public static async UniTask<TelemetryStartupTrace> Build(
            Guid startupId,
            TelemetryDevice device,
            ProfilerTraceData trace,
            bool isFinal,
            double frameBudgetMs,
            TelemetryStartupPrefix page)
        {
            var names = new Dictionary<string, string>();
            var spans = new List<TelemetryStartupSpan>(Math.Min(page.Spans.Count + trace.Spans.Count, TelemetryLimits.MaxStartupSpans));
            var bytes = 0;
            var watch = Stopwatch.StartNew();
            var shift = page.Spans.Count;

            foreach (var span in page.Spans)
            {
                if (TryAdd(spans, ref bytes, Renamed(span, Clean(span.Name, names))) == false)
                    return Trace(startupId, device, trace, isFinal, page, spans, names);
            }

            foreach (var span in trace.Spans)
            {
                var added = new TelemetryStartupSpan(
                    span.ParentId < 0 ? span.ParentId : span.ParentId + shift,
                    Clean(span.Name, names),
                    span.StartMs + page.OffsetMs,
                    span.DurationMs,
                    span.Frames,
                    span.Unfinished,
                    span.AllocBytes);

                if (TryAdd(spans, ref bytes, added) == false)
                    break;

                if (watch.Elapsed.TotalMilliseconds < frameBudgetMs)
                    continue;

                await UniTask.Yield();
                watch.Restart();
            }

            return Trace(startupId, device, trace, isFinal, page, spans, names);
        }

        private static TelemetryStartupTrace Trace(
            Guid startupId,
            TelemetryDevice device,
            ProfilerTraceData trace,
            bool isFinal,
            TelemetryStartupPrefix page,
            List<TelemetryStartupSpan> spans,
            Dictionary<string, string> names)
        {
            var durationMs = page.Spans.Count == 0
                ? trace.DurationMs
                : Math.Max(page.OffsetMs + trace.DurationMs, page.Spans[0].StartMs + page.Spans[0].DurationMs);

            return new TelemetryStartupTrace(
                startupId,
                device,
                Clean(trace.Name, names),
                isFinal,
                durationMs,
                trace.Frames,
                spans);
        }

        private static bool TryAdd(List<TelemetryStartupSpan> spans, ref int bytes, TelemetryStartupSpan span)
        {
            bytes += TelemetryLimits.StartupSpanBytes + span.Name.Length + (span.File?.Length ?? 0);

            if (spans.Count == TelemetryLimits.MaxStartupSpans || bytes > TelemetryLimits.MaxStartupBytes)
                return false;

            spans.Add(span);
            return true;
        }

        private static TelemetryStartupSpan Renamed(TelemetryStartupSpan span, string name)
        {
            return new TelemetryStartupSpan(
                span.ParentIndex,
                name,
                span.StartMs,
                span.DurationMs,
                span.Frames,
                span.IsUnfinished,
                span.AllocBytes,
                span.File,
                span.TransferBytes,
                span.IsCached);
        }

        // Names repeat through a trace and every scrubbing costs tens of expressions. Cut before it as the scrubber asks, and after
        // it: a replacement may be longer than what it replaces.
        private static string Clean(string name, Dictionary<string, string> names)
        {
            if (names.TryGetValue(name, out var clean))
                return clean;

            clean = Cut(TelemetryScrubber.Scrub(Cut(name)));
            names.Add(name, clean);

            return clean;
        }

        private static string Cut(string name)
        {
            return TelemetryLimits.Cut(name, TelemetryLimits.MaxStartupNameLength);
        }
    }
}
