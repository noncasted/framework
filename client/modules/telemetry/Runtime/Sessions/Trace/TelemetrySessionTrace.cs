using System;
using System.Collections.Generic;
using UnityEngine;

namespace Global.Telemetry
{
    // The trace of the open session outside the page: its id, tags, last marks and the main numbers of its last samples, renewed at every sample and mark and
    // removed at its close. A page killed, reloaded or frozen for good inside a session leaves it, and the next start of this device
    // reports it (SessionStart.Previous): the server sees a lost socket, this says what the client did last. The id is the random guid
    // of the server and goes as a field, never through the scrubbing of a text. Main thread only.
    public class TelemetrySessionTrace
    {
        public TelemetrySessionTrace(ITelemetryTraceStore store, ITelemetryWallClock wallClock, TelemetryNameTable names)
        {
            _store = store;
            _wallClock = wallClock;
            _names = names;
        }

        public const int MaxMarks = 8;
        public const int MaxSamples = 5;

        // What a frame did: its longest one, the main thread, the phases where the scripts and the rendering go, the frames of the
        // browser, the draw calls, the compilations of shaders. Every one is a time or a count.
        public static readonly string[] SampleNames =
        {
            "frame.dt.max",
            "frame.cpu.main",
            "frame.phase.update",
            "frame.phase.postlate",
            "web.raf.dt.max",
            "render.draw_calls",
            "web.gl.shader_compile_ms"
        };

        private const double NoValue = -1d;

        private const long NoDuration = -1;

        private readonly ITelemetryTraceStore _store;
        private readonly ITelemetryWallClock _wallClock;
        private readonly long[] _markTimes = new long[MaxMarks];
        private readonly string[] _markNames = new string[MaxMarks];
        private readonly string[] _markDetails = new string[MaxMarks];
        private readonly long[] _markDurations = new long[MaxMarks];
        private readonly TelemetryNameTable _names;
        private readonly long[] _sampleTimes = new long[MaxSamples];
        private readonly bool[] _sampleHidden = new bool[MaxSamples];
        private readonly double[] _sampleValues = new double[MaxSamples * SampleNames.Length];
        private readonly TelemetrySessionTraceData _data = new();

        private Guid _id;
        private bool _isOpen;
        private int _marks;
        private int _nextMark;
        private int _samples;
        private int _nextSample;

        // What a session that did not close left, or null. Read, not taken: the trace of the session that reports it replaces it.
        public TelemetryPreviousSession ReadPrevious()
        {
            var text = _store.Read();

            if (string.IsNullOrEmpty(text) || _isOpen)
                return null;

            return Parse(text);
        }

        public void Open(Guid id, IReadOnlyList<string> tags)
        {
            _id = id;
            _isOpen = true;
            _marks = 0;
            _nextMark = 0;
            _samples = 0;
            _nextSample = 0;
            _data.id = id.ToString();
            _data.tags = ToArray(tags);
            _data.lastSample = -1;
            Write();
        }

        // The values of a sample by the slots of the names of the session, kept until the next write.
        public void Took(long time, double[] values, bool isHidden)
        {
            if (_isOpen == false)
                return;

            _sampleTimes[_nextSample] = time;
            _sampleHidden[_nextSample] = isHidden;

            for (var i = 0; i < SampleNames.Length; i++)
            {
                var value = _names.TryFind(SampleNames[i], out var slot) ? values[slot] : double.NaN;
                _sampleValues[_nextSample * SampleNames.Length + i] = double.IsFinite(value) && value >= 0d ? value : NoValue;
            }

            _nextSample = (_nextSample + 1) % MaxSamples;
            _samples = Math.Min(_samples + 1, MaxSamples);
        }

        public void Sampled(long time)
        {
            if (_isOpen == false)
                return;

            _data.lastSample = time;
            Write();
        }

        // A detail and a duration as the buffer took them: the detail cut, null for none.
        public void Marked(long time, string name, string detail, long? durationMs)
        {
            if (_isOpen == false)
                return;

            _markTimes[_nextMark] = time;
            _markNames[_nextMark] = name;
            _markDetails[_nextMark] = detail;
            _markDurations[_nextMark] = durationMs ?? NoDuration;
            _nextMark = (_nextMark + 1) % MaxMarks;
            _marks = Math.Min(_marks + 1, MaxMarks);
            Write();
        }

        // The server has the trace of the previous session from a start whose session was over before the answer: no trace of
        // its own replaces it, so it goes, or the next start reports the same session again. Sessions start one after another,
        // so no trace is open here.
        public void Forget()
        {
            if (_isOpen)
                throw new InvalidOperationException("[Telemetry] A trace is open: it has replaced the previous one already");

            _store.Clear();
        }

        // The session closed on the client: nothing to report.
        public void Close(Guid id)
        {
            if (_isOpen == false || id != _id)
                return;

            _isOpen = false;
            Array.Clear(_markNames, 0, MaxMarks);
            Array.Clear(_markDetails, 0, MaxMarks);
            _store.Clear();
        }

        private void Write()
        {
            var first = _marks < MaxMarks ? 0 : _nextMark;

            _data.markTimes = new long[_marks];
            _data.markNames = new string[_marks];
            _data.markDetails = new string[_marks];
            _data.markDurations = new long[_marks];

            for (var i = 0; i < _marks; i++)
            {
                var slot = (first + i) % MaxMarks;

                _data.markTimes[i] = _markTimes[slot];
                _data.markNames[i] = _markNames[slot];
                _data.markDetails[i] = _markDetails[slot] ?? string.Empty;
                _data.markDurations[i] = _markDurations[slot];
            }

            WriteSamples();
            _data.writtenAt = _wallClock.NowMilliseconds;
            _store.Write(JsonUtility.ToJson(_data));
        }

        private void WriteSamples()
        {
            var first = _samples < MaxSamples ? 0 : _nextSample;

            _data.sampleNames = SampleNames;
            _data.sampleTimes = new long[_samples];
            _data.sampleHidden = new bool[_samples];
            _data.sampleValues = new double[_samples * SampleNames.Length];

            for (var i = 0; i < _samples; i++)
            {
                var slot = (first + i) % MaxSamples;

                _data.sampleTimes[i] = _sampleTimes[slot];
                _data.sampleHidden[i] = _sampleHidden[slot];
                Array.Copy(_sampleValues, slot * SampleNames.Length, _data.sampleValues, i * SampleNames.Length, SampleNames.Length);
            }
        }

        // A text of another version or a broken one is no report: dropped with one line.
        private TelemetryPreviousSession Parse(string text)
        {
            TelemetrySessionTraceData data;

            try
            {
                data = JsonUtility.FromJson<TelemetrySessionTraceData>(text);
            }
            catch (ArgumentException)
            {
                data = null;
            }

            if (data == null || Guid.TryParse(data.id, out var id) == false)
            {
                _store.Clear();
                TelemetrySelfLog.Line("The trace of the previous session is not readable: dropped");
                return null;
            }

            var count = Math.Min(data.markTimes?.Length ?? 0, data.markNames?.Length ?? 0);
            var marks = new TelemetryBatchMark[count];

            for (var i = 0; i < count; i++)
                marks[i] = new TelemetryBatchMark(data.markTimes[i], data.markNames[i], DetailAt(data, i), DurationAt(data, i));

            var since = _wallClock.NowMilliseconds - data.writtenAt;

            var (sampleNames, samples) = SamplesOf(data);

            return new TelemetryPreviousSession(id, data.tags ?? Array.Empty<string>(), data.lastSample, since >= 0 ? since : -1, marks, sampleNames, samples);
        }

        // A trace written before the samples, or one with arrays that do not agree, has none.
        private static (string[] Names, TelemetryBatchSample[] Samples) SamplesOf(TelemetrySessionTraceData data)
        {
            var names = data.sampleNames;
            var count = data.sampleTimes?.Length ?? 0;

            if (names == null || names.Length == 0 || count == 0 || data.sampleHidden?.Length != count || data.sampleValues?.Length != count * names.Length)
                return (Array.Empty<string>(), Array.Empty<TelemetryBatchSample>());

            var samples = new TelemetryBatchSample[count];

            for (var i = 0; i < count; i++)
            {
                var values = new double[names.Length];

                for (var j = 0; j < names.Length; j++)
                {
                    var value = data.sampleValues[i * names.Length + j];
                    values[j] = value >= 0d ? value : double.NaN;
                }

                samples[i] = new TelemetryBatchSample(data.sampleTimes[i], values, data.sampleHidden[i]);
            }

            return (names, samples);
        }

        // A trace written before the marks had a detail has neither array.
        private static string DetailAt(TelemetrySessionTraceData data, int index)
        {
            var detail = data.markDetails != null && index < data.markDetails.Length ? data.markDetails[index] : null;

            return string.IsNullOrEmpty(detail) ? null : detail;
        }

        private static long? DurationAt(TelemetrySessionTraceData data, int index)
        {
            var duration = data.markDurations != null && index < data.markDurations.Length ? data.markDurations[index] : NoDuration;

            return duration >= 0 ? duration : (long?)null;
        }

        private static string[] ToArray(IReadOnlyList<string> tags)
        {
            var array = new string[tags.Count];

            for (var i = 0; i < tags.Count; i++)
                array[i] = tags[i];

            return array;
        }
    }
}
