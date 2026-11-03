using System;
using System.Collections.Generic;
using UnityEngine;

namespace Global.Telemetry
{
    // The trace of the open session outside the page: its id, tags, last sample and last marks, renewed at every sample and mark and
    // removed at its close. A page killed, reloaded or frozen for good inside a session leaves it, and the next start of this device
    // reports it (SessionStart.Previous): the server sees a lost socket, this says what the client did last. The id is the random guid
    // of the server and goes as a field, never through the scrubbing of a text. Main thread only.
    public class TelemetrySessionTrace
    {
        public TelemetrySessionTrace(ITelemetryTraceStore store, ITelemetryWallClock wallClock)
        {
            _store = store;
            _wallClock = wallClock;
        }

        public const int MaxMarks = 8;

        private readonly ITelemetryTraceStore _store;
        private readonly ITelemetryWallClock _wallClock;
        private readonly long[] _markTimes = new long[MaxMarks];
        private readonly string[] _markNames = new string[MaxMarks];
        private readonly TelemetrySessionTraceData _data = new();

        private Guid _id;
        private bool _isOpen;
        private int _marks;
        private int _nextMark;

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
            _data.id = id.ToString();
            _data.tags = ToArray(tags);
            _data.lastSample = -1;
            Write();
        }

        public void Sampled(long time)
        {
            if (_isOpen == false)
                return;

            _data.lastSample = time;
            Write();
        }

        public void Marked(long time, string name)
        {
            if (_isOpen == false)
                return;

            _markTimes[_nextMark] = time;
            _markNames[_nextMark] = name;
            _nextMark = (_nextMark + 1) % MaxMarks;
            _marks = Math.Min(_marks + 1, MaxMarks);
            Write();
        }

        // The session closed on the client: nothing to report.
        public void Close(Guid id)
        {
            if (_isOpen == false || id != _id)
                return;

            _isOpen = false;
            Array.Clear(_markNames, 0, MaxMarks);
            _store.Clear();
        }

        private void Write()
        {
            var first = _marks < MaxMarks ? 0 : _nextMark;

            _data.markTimes = new long[_marks];
            _data.markNames = new string[_marks];

            for (var i = 0; i < _marks; i++)
            {
                _data.markTimes[i] = _markTimes[(first + i) % MaxMarks];
                _data.markNames[i] = _markNames[(first + i) % MaxMarks];
            }

            _data.writtenAt = _wallClock.NowMilliseconds;
            _store.Write(JsonUtility.ToJson(_data));
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
                marks[i] = new TelemetryBatchMark(data.markTimes[i], data.markNames[i]);

            var since = _wallClock.NowMilliseconds - data.writtenAt;

            return new TelemetryPreviousSession(id, data.tags ?? Array.Empty<string>(), data.lastSample, since >= 0 ? since : -1, marks);
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
