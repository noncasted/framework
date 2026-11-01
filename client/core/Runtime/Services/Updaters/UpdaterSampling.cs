using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    // The time of the targets of the Updater by their type, opt-in (a profiling tool of a game turns it on). Off it
    // costs one check a call. A sample sums the calls of the type since the last Reset: Ticks of the Stopwatch.
    [NoAutoStaticsCleanup]
    public static class UpdaterSampling
    {
        private static readonly Dictionary<Type, UpdaterSample> _samples = new();

        public static bool IsEnabled { get; private set; }

        public static IReadOnlyDictionary<Type, UpdaterSample> Samples => _samples;

        public static void Enable()
        {
            IsEnabled = true;
        }

        public static void Disable()
        {
            IsEnabled = false;
            _samples.Clear();
        }

        // Zeroes the samples, the types stay.
        public static void Reset()
        {
            foreach (var sample in _samples.Values)
            {
                sample.Reset();
            }
        }

        // 0 when off: End skips it.
        public static long Begin()
        {
            return IsEnabled == true ? Stopwatch.GetTimestamp() : 0;
        }

        public static void End(object target, long start)
        {
            if (start == 0 || IsEnabled == false)
                return;

            End(target.GetType(), start);
        }

        // A proxy updater of a game passes the type the time belongs to (the owner of a callback).
        public static void End(Type type, long start)
        {
            if (start == 0 || IsEnabled == false)
                return;

            var ticks = Stopwatch.GetTimestamp() - start;

            if (_samples.TryGetValue(type, out var sample) == false)
            {
                sample = new UpdaterSample();
                _samples.Add(type, sample);
            }

            sample.Add(ticks);
        }
    }

    public class UpdaterSample
    {
        public long Ticks { get; private set; }
        public int Calls { get; private set; }

        internal void Add(long ticks)
        {
            Ticks += ticks;
            Calls++;
        }

        internal void Reset()
        {
            Ticks = 0;
            Calls = 0;
        }
    }
}
