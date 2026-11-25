using System;
using System.Collections.Generic;
using System.Linq;

namespace Global.Telemetry
{
    // The structures of the module as the HTTP JSON of the contract: a copy, the message may wait in the queue of the gate.
    // JsonUtility writes no null: an absent text is empty, an absent number 0, an absent object is said by a has* flag. A guid is
    // always its text, Guid.Empty for none: the server reads it as a guid.
    public static class TelemetryHttpMessages
    {
        public static TelemetryHttpSessionStart ToSessionStart(Guid linkId, TelemetrySessionStart start)
        {
            return new TelemetryHttpSessionStart
            {
                linkId = linkId.ToString(),
                device = ToDevice(start.Device),
                tags = start.Tags.ToArray(),
                startupId = start.StartupId.ToString(),
                hasPrevious = start.Previous != null,
                previous = start.Previous == null ? new TelemetryHttpPreviousSession { sessionId = Guid.Empty.ToString() } : ToPrevious(start.Previous)
            };
        }

        public static TelemetryHttpBatch ToBatch(Guid linkId, Guid sessionId, TelemetryBatch batch)
        {
            return new TelemetryHttpBatch
            {
                linkId = linkId.ToString(),
                sessionId = sessionId.ToString(),
                names = Map(batch.Names, ToName),
                samples = Map(batch.Samples, ToSample),
                errors = Map(batch.Errors, ToError),
                marks = Map(batch.Marks, ToMark)
            };
        }

        public static TelemetryHttpSessionEnd ToSessionEnd(Guid linkId, Guid sessionId)
        {
            return new TelemetryHttpSessionEnd
            {
                linkId = linkId.ToString(),
                sessionId = sessionId.ToString()
            };
        }

        public static TelemetryHttpStartupTrace ToStartupTrace(Guid linkId, TelemetryStartupTrace trace)
        {
            return new TelemetryHttpStartupTrace
            {
                linkId = linkId.ToString(),
                startupId = trace.StartupId.ToString(),
                device = ToDevice(trace.Device),
                name = trace.Name,
                final = trace.IsFinal,
                durationMs = trace.DurationMs,
                frames = trace.Frames,
                spans = Map(trace.Spans, ToStartupSpan)
            };
        }

        // A value that is not finite is no value: the batch writer already turns infinities into NaN, the trace of a previous
        // session keeps NaN for none.
        public static TelemetryHttpSample ToSample(TelemetryBatchSample sample)
        {
            var values = new double[sample.Values.Count];
            var empty = new List<int>();

            for (var i = 0; i < values.Length; i++)
            {
                var value = sample.Values[i];

                if (double.IsFinite(value))
                    values[i] = value;
                else
                    empty.Add(i);
            }

            return new TelemetryHttpSample
            {
                time = sample.Time,
                values = values,
                empty = empty.ToArray(),
                hidden = sample.IsHidden
            };
        }

        public static string ToLevel(TelemetryErrorLevel level)
        {
            return level switch
            {
                TelemetryErrorLevel.Error => "Error",
                TelemetryErrorLevel.Exception => "Exception",
                TelemetryErrorLevel.Warning => "Warning",
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, "[Telemetry] Unknown level of an error")
            };
        }

        private static TelemetryHttpPreviousSession ToPrevious(TelemetryPreviousSession previous)
        {
            return new TelemetryHttpPreviousSession
            {
                sessionId = previous.SessionId.ToString(),
                tags = previous.Tags.ToArray(),
                lastSampleTime = previous.LastSampleTime,
                sinceMs = previous.SinceMs,
                marks = Map(previous.Marks, ToMark),
                sampleNames = previous.SampleNames.ToArray(),
                samples = Map(previous.Samples, ToSample)
            };
        }

        private static TelemetryHttpDevice ToDevice(TelemetryDevice device)
        {
            return new TelemetryHttpDevice
            {
                platform = device.Platform,
                buildVersion = device.BuildVersion,
                browser = device.Browser,
                browserMajor = device.BrowserMajor ?? 0,
                os = device.Os,
                osMajor = device.OsMajor ?? 0,
                gpu = device.Gpu,
                cores = device.Cores ?? 0,
                memoryClass = device.MemoryClass ?? 0,
                screenWidth = device.ScreenWidth ?? 0,
                screenHeight = device.ScreenHeight ?? 0,
                dpr = device.Dpr ?? 0
            };
        }

        private static TelemetryHttpStartupSpan ToStartupSpan(TelemetryStartupSpan span)
        {
            return new TelemetryHttpStartupSpan
            {
                parentIndex = span.ParentIndex,
                name = span.Name,
                startMs = span.StartMs,
                durationMs = span.DurationMs,
                frames = span.Frames,
                unfinished = span.IsUnfinished,
                allocBytes = span.AllocBytes,
                file = span.File,
                transferBytes = span.TransferBytes,
                cached = span.IsCached
            };
        }

        private static TelemetryHttpName ToName(TelemetryBatchName name)
        {
            return new TelemetryHttpName
            {
                name = name.Name,
                unit = name.Unit
            };
        }

        private static TelemetryHttpError ToError(TelemetryBatchError error)
        {
            return new TelemetryHttpError
            {
                time = error.Time,
                level = ToLevel(error.Level),
                message = error.Message,
                stack = error.Stack,
                repeat = error.Repeat,
                category = error.Category
            };
        }

        private static TelemetryHttpMark ToMark(TelemetryBatchMark mark)
        {
            return new TelemetryHttpMark
            {
                time = mark.Time,
                name = mark.Name,
                detail = mark.Detail,
                hasDuration = mark.DurationMs.HasValue,
                durationMs = mark.DurationMs ?? 0
            };
        }

        private static TOut[] Map<TIn, TOut>(IReadOnlyList<TIn> items, Func<TIn, TOut> convert)
        {
            var result = new TOut[items.Count];

            for (var i = 0; i < result.Length; i++)
                result[i] = convert(items[i]);

            return result;
        }
    }
}
