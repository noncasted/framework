using System;
using System.Collections.Generic;

namespace Global.Telemetry
{
    // The next batch of the session, under the limits of the contract. What gets written leaves the buffer; what does not
    // fit stays for the next batch.
    public class TelemetryBatchWriter
    {
        public TelemetryBatchWriter(TelemetryNameTable names, TelemetryBuffer buffer)
        {
            _names = names;
            _buffer = buffer;
        }

        // The size counted is the worst case on the wire: the text of an error may take 3 bytes of UTF-8 per character (names
        // and marks are ASCII by their rule), every record has a few bytes around it, the batch a few more.
        private const int _batchBytes = 256;
        private const int _recordBytes = 32;
        private const int _errorCharBytes = 3;

        private readonly TelemetryNameTable _names;
        private readonly TelemetryBuffer _buffer;

        private int _budget;

        // Null: nothing new.
        public TelemetryBatch WriteNext()
        {
            if (HasNews() == false)
                return null;

            _budget = TelemetryLimits.MaxBatchBytes - _batchBytes;

            // In this order: a sample must not go before its name.
            var names = WriteNames();
            var marks = WriteMarks();
            var errors = WriteErrors();
            var samples = WriteSamples();

            return new TelemetryBatch(names, samples, marks, errors);
        }

        // The whole rest: each batch takes at least the oldest of everything, so this ends.
        public IReadOnlyList<TelemetryBatch> WriteAll()
        {
            var batches = new List<TelemetryBatch>();

            for (var batch = WriteNext(); batch != null; batch = WriteNext())
                batches.Add(batch);

            return batches;
        }

        private bool HasNews()
        {
            return _names.Written < _names.Count
                   || _buffer.Samples.Count > 0
                   || _buffer.Marks.Count > 0
                   || _buffer.Errors.Count > 0;
        }

        // Not limited by the budget: at most 200 short names, and a sample without its name means nothing to the server.
        private IReadOnlyList<TelemetryBatchName> WriteNames()
        {
            var names = new List<TelemetryBatchName>(_names.Count - _names.Written);

            for (var i = _names.Written; i < _names.Count; i++)
            {
                var name = new TelemetryBatchName(_names.NameAt(i), _names.UnitAt(i));
                _budget -= _recordBytes + name.Name.Length + (name.Unit?.Length ?? 0);
                names.Add(name);
            }

            _names.MarkAllWritten();
            return names;
        }

        private IReadOnlyList<TelemetryBatchMark> WriteMarks()
        {
            var marks = _buffer.Marks;
            var written = new List<TelemetryBatchMark>();

            while (written.Count < marks.Count && TryTake(_recordBytes + marks.At(written.Count).Name.Length + (marks.At(written.Count).Detail?.Length ?? 0)))
            {
                var mark = marks.At(written.Count);
                written.Add(new TelemetryBatchMark(mark.Time, mark.Name, mark.Detail, mark.DurationMs));
            }

            marks.RemoveOldest(written.Count);
            return written;
        }

        private IReadOnlyList<TelemetryBatchError> WriteErrors()
        {
            var errors = _buffer.Errors;
            var written = new List<TelemetryBatchError>();

            while (written.Count < errors.Count && TryTake(SizeOf(errors.At(written.Count))))
            {
                var error = errors.At(written.Count);
                written.Add(new TelemetryBatchError(error.Time, error.Level, error.Category, error.Message, error.Stack, error.Repeat));
            }

            errors.RemoveOldest(written.Count);
            return written;
        }

        private IReadOnlyList<TelemetryBatchSample> WriteSamples()
        {
            var samples = _buffer.Samples;
            var limit = Math.Min(samples.Count, TelemetryLimits.MaxSamplesPerBatch);
            var written = new List<TelemetryBatchSample>();

            while (written.Count < limit && TryTake(_recordBytes + samples.At(written.Count).Count * sizeof(double)))
                written.Add(Copy(samples.At(written.Count)));

            samples.RemoveOldest(written.Count);
            return written;
        }

        private static int SizeOf(TelemetryErrorRecord error)
        {
            return _recordBytes + _errorCharBytes * (error.Message.Length + (error.Stack?.Length ?? 0) + (error.Category?.Length ?? 0));
        }

        // A non-finite value counts as no value in this sample, the contract has only NaN for that.
        private static TelemetryBatchSample Copy(TelemetrySampleRecord sample)
        {
            var values = new double[sample.Count];

            for (var i = 0; i < values.Length; i++)
            {
                var value = sample.ValueAt(i);
                values[i] = double.IsInfinity(value) ? double.NaN : value;
            }

            return new TelemetryBatchSample(sample.Time, values, sample.IsHidden);
        }

        private bool TryTake(int bytes)
        {
            if (bytes > _budget)
                return false;

            _budget -= bytes;
            return true;
        }
    }
}
