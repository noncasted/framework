using System;
using System.Diagnostics;

namespace Internal
{
    // One load of content: a catalogue group or a scene of a scope. Key is a name of the code ("Group: MusicRunAudio",
    // "Scene: Run"), never an address or a path; a scene learns its name only when it is loaded.
    public class AssetLoad
    {
        public AssetLoad(string key)
        {
            Key = key;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        private readonly long _startTimestamp;
        private long _endTimestamp;

        public string Key { get; private set; }
        public bool IsOver { get; private set; }

        // Null unless the load failed.
        public Exception Error { get; private set; }

        // Up to now while it goes on, the whole load when it is over.
        public double ElapsedMs => ((IsOver ? _endTimestamp : Stopwatch.GetTimestamp()) - _startTimestamp) * 1000d / Stopwatch.Frequency;

        public void Finish(string key)
        {
            Key = key;
            End(null);
        }

        public void Finish()
        {
            End(null);
        }

        public void Fail(Exception error)
        {
            End(error);
        }

        private void End(Exception error)
        {
            if (IsOver)
                throw new InvalidOperationException($"[AssetLoad] {Key} is already over");

            _endTimestamp = Stopwatch.GetTimestamp();
            Error = error;
            IsOver = true;
            AssetLoads.OnEnded(this);
        }
    }
}
