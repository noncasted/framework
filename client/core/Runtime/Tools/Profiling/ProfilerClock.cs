using Unity.Profiling.LowLevel.Unsafe;

namespace Internal
{
    /// <summary>
    /// Unity profiler clock. With it trace spans are placed on the same frames the
    /// profiler recorded: frame numbers on the two sides do not match (the profiler
    /// counts its own and skips those that were not recorded), but the time is shared.
    /// </summary>
    public static class ProfilerClock
    {
        /// <summary>Current profiler clock time in nanoseconds. 0 if the build has no profiler.</summary>
        public static long NowNs()
        {
#if ENABLE_PROFILER
            var ratio = ProfilerUnsafeUtility.TimestampToNanosecondsConversionRatio;
            var timestamp = ProfilerUnsafeUtility.Timestamp;

            if (ratio.Denominator <= 0L)
                return 0L;

            // A straight timestamp * Numerator overflows long: ticks count from machine start,
            // so the remainder is multiplied separately.
            return timestamp / ratio.Denominator * ratio.Numerator +
                   timestamp % ratio.Denominator * ratio.Numerator / ratio.Denominator;
#else
            return 0L;
#endif
        }
    }
}