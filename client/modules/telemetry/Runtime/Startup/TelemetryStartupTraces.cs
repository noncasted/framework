using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Telemetry
{
    // A trace of the profiler as the module sends it: within the limits of the server, the names cleaned, the time only from the
    // start of the trace. Its spans go depth first and a span id is its index, so a tail cut leaves every parent in place.
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
            double frameBudgetMs)
        {
            var names = new Dictionary<string, string>();
            var spans = new List<TelemetryStartupSpan>(Math.Min(trace.Spans.Count, TelemetryLimits.MaxStartupSpans));
            var bytes = 0;
            var watch = Stopwatch.StartNew();

            foreach (var span in trace.Spans)
            {
                var name = Clean(span.Name, names);

                bytes += TelemetryLimits.StartupSpanBytes + name.Length;

                if (spans.Count == TelemetryLimits.MaxStartupSpans || bytes > TelemetryLimits.MaxStartupBytes)
                    break;

                spans.Add(new TelemetryStartupSpan(
                    span.ParentId,
                    name,
                    span.StartMs,
                    span.DurationMs,
                    span.Frames,
                    span.Unfinished,
                    span.AllocBytes));

                if (watch.Elapsed.TotalMilliseconds < frameBudgetMs)
                    continue;

                await UniTask.Yield();
                watch.Restart();
            }

            return new TelemetryStartupTrace(
                startupId,
                device,
                Clean(trace.Name, names),
                isFinal,
                trace.DurationMs,
                trace.Frames,
                spans);
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
