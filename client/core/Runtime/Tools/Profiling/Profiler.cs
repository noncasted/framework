using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Internal
{
    public class Profiler : IProfiler
    {
        public Profiler(string name)
        {
            Name = name;
            _startedAt = DateTime.Now;
            _startFrame = Time.frameCount;
            _startNs = ProfilerClock.NowNs();
            _watch = Stopwatch.StartNew();
            StartTimestamp = Stopwatch.GetTimestamp();
        }

        private static readonly ILog _log = Logs.Of<Profiler>();

        private readonly DateTime _startedAt;
        private readonly int _startFrame;
        private readonly long _startNs;
        private readonly Stopwatch _watch;
        private readonly List<ProfilerScope> _roots = new();

        private bool _finished;

        public string Name { get; }

        // The start by Stopwatch.GetTimestamp: places the trace on another clock that runs now (the time line of a web page).
        public long StartTimestamp { get; }

        public double ElapsedMs => _watch.Elapsed.TotalMilliseconds;

        /// <summary>Engine frame the trace started on. Absolute.</summary>
        public int StartFrame => _startFrame;

        /// <summary>Frames since the trace start. Shows whether a span waited for frames or computed.</summary>
        public int ElapsedFrames => Time.frameCount - _startFrame;

        /// <summary>The closed trace as it was saved. Null until <see cref="Finish"/>.</summary>
        public ProfilerTraceData Final { get; private set; }

        public IProfilerScope CreateScope(string name)
        {
            var scope = new ProfilerScope(this, name, 0);
            _roots.Add(scope);

            return scope;
        }

        public void Finish()
        {
            if (_finished)
                return;

            _finished = true;

            foreach (var root in _roots)
                root.StopRecursive();

            _watch.Stop();

            var trace = Build();
            Final = trace;
            ProfilerTraceStorage.Save(trace);

            _log.Info("{Name} finished in {DurationMs} ms / {Frames} frames, {Spans} spans",
                Name, Math.Round(trace.DurationMs, 1), trace.Frames, trace.Spans.Count);
        }

        public ProfilerTraceData Build()
        {
            foreach (var root in _roots)
                root.CoverChildren();

            return Flatten();
        }

        /// <summary>
        /// The trace as it is now, while it goes on: changes nothing in it, so what <see cref="Finish"/> saves is the same with
        /// or without a snapshot. Open spans have the duration so far and are <see cref="ProfilerSpanData.Unfinished"/>; a
        /// parent does not cover its children yet, that is done once, at the finish.
        /// </summary>
        public ProfilerTraceData Snapshot()
        {
            return Flatten();
        }

        private ProfilerTraceData Flatten()
        {
            var trace = new ProfilerTraceData
            {
                Name = Name,
                StartedAt = _startedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                DurationMs = _watch.Elapsed.TotalMilliseconds,
                Frames = ElapsedFrames,
                StartFrame = _startFrame,
                EndFrame = Time.frameCount,
                StartNs = _startNs,
                EndNs = ProfilerClock.NowNs(),
                Cold = ProfilerTraceStorage.ColdDomain,
                Spans = new List<ProfilerSpanData>()
            };

            var nextId = 0;

            // Traversal order is depth-first by start time: this way rows in the window go
            // top to bottom exactly as the stages went in time.
            foreach (var root in ByStart(_roots))
                Append(root, -1, 0);

            return trace;

            void Append(ProfilerScope scope, int parentId, int depth)
            {
                var id = nextId++;

                trace.Spans.Add(new ProfilerSpanData
                {
                    Id = id,
                    ParentId = parentId,
                    Depth = depth,
                    Name = scope.Name,
                    StartMs = Math.Max(0d, scope.StartMs),
                    DurationMs = scope.DurationMs,
                    Frames = scope.Frames,
                    StartFrame = scope.StartFrame,
                    EndFrame = scope.EndFrame,
                    StartNs = scope.StartNs,
                    EndNs = scope.EndNs,
                    Unfinished = scope.WasUnfinished || scope.IsRunning,
                    AllocBytes = scope.AllocBytes
                });

                foreach (var child in ByStart(scope.ChildrenInternal))
                    Append(child, id, depth + 1);
            }

            static List<ProfilerScope> ByStart(List<ProfilerScope> scopes)
            {
                var sorted = new List<ProfilerScope>(scopes);

                sorted.Sort((left, right) => left.StartMs.CompareTo(right.StartMs));

                return sorted;
            }
        }
    }
}