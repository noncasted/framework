using System;
using Shared;

namespace Global.MatchMetrics
{
    // A batch as the message of the contract, built at every send: the ages are counted to the moment it goes.
    public static class MatchMetricsMessages
    {
        public static SharedMatchMetrics.Batch ToBatch(Guid streamId, MatchMetricsBatchRecord batch, double nowMs)
        {
            var series = new SharedMatchMetrics.MatchSeriesDecl[batch.Series.Count];

            for (var i = 0; i < series.Length; i++)
                series[i] = ToSeries(batch.Series[i]);

            var chunks = new SharedMatchMetrics.MatchChunk[batch.Chunks.Count];

            for (var i = 0; i < chunks.Length; i++)
            {
                var chunk = batch.Chunks[i];

                chunks[i] = new SharedMatchMetrics.MatchChunk
                {
                    Slot = chunk.Slot,
                    Start = chunk.Start,
                    AgeMs = AgeOf(chunk.StartMs, nowMs),
                    Values = chunk.Values
                };
            }

            var events = new SharedMatchMetrics.MatchEvent[batch.Events.Count];

            for (var i = 0; i < events.Length; i++)
                events[i] = ToEvent(batch.Events[i], nowMs);

            return new SharedMatchMetrics.Batch
            {
                StreamId = streamId,
                Seq = batch.Seq,
                Series = series,
                Chunks = chunks,
                Events = events
            };
        }

        private static SharedMatchMetrics.MatchSeriesDecl ToSeries(MatchSeriesRecord record)
        {
            var keys = new string[record.Tags.Count];
            var values = new string[record.Tags.Count];

            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = record.Tags.GetKey(i);
                values[i] = record.Tags.GetValue(i);
            }

            return new SharedMatchMetrics.MatchSeriesDecl
            {
                Slot = record.Slot,
                Name = record.Name,
                Kind = record.Kind,
                TagKeys = keys,
                TagValues = values
            };
        }

        private static SharedMatchMetrics.MatchEvent ToEvent(MatchEventRecord record, double nowMs)
        {
            var fields = new SharedMatchMetrics.MatchEventField[record.Fields.Count];

            for (var i = 0; i < fields.Length; i++)
            {
                var field = record.Fields[i];

                fields[i] = new SharedMatchMetrics.MatchEventField
                {
                    Key = field.Key,
                    Number = field.Number,
                    Text = field.Text
                };
            }

            return new SharedMatchMetrics.MatchEvent
            {
                Name = record.Name,
                Time = record.Time,
                AgeMs = AgeOf(record.AtMs, nowMs),
                Fields = fields
            };
        }

        private static long AgeOf(double startMs, double nowMs)
        {
            return (long)Math.Max(0d, nowMs - startMs);
        }
    }
}
