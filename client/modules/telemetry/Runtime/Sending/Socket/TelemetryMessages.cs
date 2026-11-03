using System;
using System.Collections.Generic;
using System.Linq;
using Shared;

namespace Global.Telemetry
{
    // The structures of the module as the messages of the contract (shared/ClientTelemetry). A copy: the message may wait in
    // the queue of the writer for its frame.
    public static class TelemetryMessages
    {
        public static SharedClientTelemetry.SessionStart ToSessionStart(TelemetrySessionStart start)
        {
            return new SharedClientTelemetry.SessionStart
            {
                Device = ToDevice(start.Device),
                Tags = start.Tags.ToArray(),
                StartupId = start.StartupId,
                Previous = start.Previous == null ? null : ToPrevious(start.Previous)
            };
        }

        private static SharedClientTelemetry.PreviousSession ToPrevious(TelemetryPreviousSession previous)
        {
            return new SharedClientTelemetry.PreviousSession
            {
                SessionId = previous.SessionId,
                Tags = previous.Tags.ToArray(),
                LastSampleTime = previous.LastSampleTime,
                SinceMs = previous.SinceMs,
                Marks = Map(previous.Marks, ToMark)
            };
        }

        public static SharedClientTelemetry.Batch ToBatch(Guid sessionId, TelemetryBatch batch)
        {
            return new SharedClientTelemetry.Batch
            {
                SessionId = sessionId,
                Names = Map(batch.Names, ToName),
                Samples = Map(batch.Samples, ToSample),
                Errors = Map(batch.Errors, ToError),
                Marks = Map(batch.Marks, ToMark)
            };
        }

        public static SharedClientTelemetry.StartupTrace ToStartupTrace(TelemetryStartupTrace trace)
        {
            return new SharedClientTelemetry.StartupTrace
            {
                StartupId = trace.StartupId,
                Device = ToDevice(trace.Device),
                Name = trace.Name,
                Final = trace.IsFinal,
                DurationMs = trace.DurationMs,
                Frames = trace.Frames,
                Spans = Map(trace.Spans, ToStartupSpan)
            };
        }

        // By name: the numbers of the two enums differ.
        public static SharedClientTelemetry.ErrorLevel ToLevel(TelemetryErrorLevel level)
        {
            return level switch
            {
                TelemetryErrorLevel.Error => SharedClientTelemetry.ErrorLevel.Error,
                TelemetryErrorLevel.Exception => SharedClientTelemetry.ErrorLevel.Exception,
                TelemetryErrorLevel.Warning => SharedClientTelemetry.ErrorLevel.Warning,
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, "[Telemetry] Unknown level of an error")
            };
        }

        // The wire has no null for a number: 0 is unknown by the contract.
        private static SharedClientTelemetry.Device ToDevice(TelemetryDevice device)
        {
            return new SharedClientTelemetry.Device
            {
                Platform = device.Platform,
                BuildVersion = device.BuildVersion,
                Browser = device.Browser,
                BrowserMajor = device.BrowserMajor ?? 0,
                Os = device.Os,
                OsMajor = device.OsMajor ?? 0,
                Gpu = device.Gpu,
                Cores = device.Cores ?? 0,
                MemoryClass = device.MemoryClass ?? 0,
                ScreenWidth = device.ScreenWidth ?? 0,
                ScreenHeight = device.ScreenHeight ?? 0,
                Dpr = device.Dpr ?? 0
            };
        }

        private static SharedClientTelemetry.StartupSpan ToStartupSpan(TelemetryStartupSpan span)
        {
            return new SharedClientTelemetry.StartupSpan
            {
                ParentIndex = span.ParentIndex,
                Name = span.Name,
                StartMs = span.StartMs,
                DurationMs = span.DurationMs,
                Frames = span.Frames,
                Unfinished = span.IsUnfinished,
                AllocBytes = span.AllocBytes,
                File = span.File,
                TransferBytes = span.TransferBytes,
                Cached = span.IsCached
            };
        }

        private static SharedClientTelemetry.MetricName ToName(TelemetryBatchName name)
        {
            return new SharedClientTelemetry.MetricName
            {
                Name = name.Name,
                Unit = name.Unit
            };
        }

        private static SharedClientTelemetry.Sample ToSample(TelemetryBatchSample sample)
        {
            return new SharedClientTelemetry.Sample
            {
                Time = sample.Time,
                Values = sample.Values.ToArray(),
                Hidden = sample.IsHidden
            };
        }

        private static SharedClientTelemetry.Error ToError(TelemetryBatchError error)
        {
            return new SharedClientTelemetry.Error
            {
                Time = error.Time,
                Level = ToLevel(error.Level),
                Message = error.Message,
                Stack = error.Stack,
                Repeat = error.Repeat
            };
        }

        private static SharedClientTelemetry.Mark ToMark(TelemetryBatchMark mark)
        {
            return new SharedClientTelemetry.Mark
            {
                Time = mark.Time,
                Name = mark.Name,
                Detail = mark.Detail,
                DurationMs = mark.DurationMs
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
