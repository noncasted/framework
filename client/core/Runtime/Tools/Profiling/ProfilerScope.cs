using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    public class ProfilerScope : IProfilerScope
    {
        public ProfilerScope(Profiler profiler, string name, int depth)
        {
            _profiler = profiler;
            Name = name;
            Depth = depth;
        }

        private readonly Profiler _profiler;
        private readonly List<ProfilerScope> _children = new();

        private double _startMs = -1d;
        private double _endMs = -1d;

        private int _startFrame = -1;
        private int _endFrame = -1;

        private long _startNs;
        private long _endNs;

        private long _startAlloc;
        private long _endAlloc = -1L;

        public string Name { get; private set; }
        public int Depth { get; }

        /// <summary>
        /// Engine frame the span opened on. Absolute (<see cref="UnityEngine.Time.frameCount"/>),
        /// not relative to the trace start: the editor uses it to fetch profiler samples of exactly the frames
        /// the scope took.
        /// </summary>
        public int StartFrame => _startFrame;

        /// <summary>Frame the span closed on. -1 until the scope is stopped.</summary>
        public int EndFrame => _endFrame;

        /// <summary>Span bounds by the profiler clock, ns: the editor finds its frames by them.</summary>
        public long StartNs => _startNs;

        public long EndNs => _endNs;

        /// <summary>
        /// Managed memory the main thread allocated during the span, bytes. Counted by the thread counter, not by
        /// the heap, so a garbage collection in the middle of the span does not spoil the number. A span that waits
        /// for frames gets everything allocated while it waited. Always 0 outside the editor.
        /// </summary>
        public long AllocBytes
        {
            get
            {
                if (_startMs < 0d)
                    return 0L;

                var end = _endAlloc < 0L ? ContainerDiagnostics.AllocatedBytes() : _endAlloc;

                return end - _startAlloc;
            }
        }

        public bool IsRunning => _startMs >= 0d && _endMs < 0d;

        public double StartMs => _startMs;

        public double DurationMs
        {
            get
            {
                if (_startMs < 0d)
                    return 0d;

                var end = _endMs < 0d ? _profiler.ElapsedMs : _endMs;

                return end - _startMs;
            }
        }

        public int Frames
        {
            get
            {
                if (_startFrame < 0)
                    return 0;

                var end = _endFrame < 0 ? Time.frameCount : _endFrame;

                return end - _startFrame;
            }
        }

        /// <summary>A scope that was not closed manually: closed on Finish, the duration is approximate.</summary>
        public bool WasUnfinished { get; private set; }

        public IReadOnlyList<IProfilerScope> Children => _children;

        internal List<ProfilerScope> ChildrenInternal => _children;

        public IProfilerScope Child(string name)
        {
            var child = new ProfilerScope(_profiler, name, Depth + 1);
            _children.Add(child);

            return child;
        }

        public void SetName(string name)
        {
            Name = name;
        }

        public void Start()
        {
            if (_startMs >= 0d)
                return;

            _startMs = _profiler.ElapsedMs;
            _startFrame = Time.frameCount;
            _startNs = ProfilerClock.NowNs();
            _startAlloc = ContainerDiagnostics.AllocatedBytes();
        }

        public void Stop()
        {
            if (_startMs < 0d || _endMs >= 0d)
                return;

            // The counter is read first: the other measurements of the stop must not get into it.
            _endAlloc = ContainerDiagnostics.AllocatedBytes();
            _endMs = _profiler.ElapsedMs;
            _endFrame = Time.frameCount;
            _endNs = ProfilerClock.NowNs();
        }

        public void Dispose()
        {
            Stop();
        }

        internal void StopRecursive()
        {
            if (IsRunning)
            {
                WasUnfinished = true;
                Stop();
            }

            foreach (var child in _children)
                child.StopRecursive();
        }
    }
}