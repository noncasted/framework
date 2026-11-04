using System;
using NUnit.Framework;

namespace Internal
{
    // The guard of a hot path (a frame of the game, a step of a simulation, a send of the network): once warmed up it
    // allocates nothing, so a player with a stop-the-world GC (WebGL: Boehm, not incremental) never collects in the middle
    // of a fight. Counts the managed bytes of the current thread: the pass runs on the thread of the test.
    public static class FrameAllocations
    {
        public const int DefaultWarmup = 10;

        // The bytes allocated by `passes` runs of the pass after `warmup` runs that are not counted (the lists grow, the
        // comparers and caches are made on the first ones). Asserts stay out of the pass: NUnit allocates in them.
        public static long Measure(int passes, Action pass, int warmup = DefaultWarmup)
        {
            if (passes <= 0)
                throw new ArgumentOutOfRangeException(nameof(passes), passes, "at least one measured pass");

            for (var i = 0; i < warmup; i++)
                pass.Invoke();

            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < passes; i++)
                pass.Invoke();

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        // Fails when the passes allocate: below a byte a pass is nothing per pass (the runtime allocates once now and
        // then, 48 B seen), the smallest object is 16-24 B, so any allocation of every pass is caught.
        public static void AssertNone(string what, int passes, Action pass, int warmup = DefaultWarmup)
        {
            var bytes = Measure(passes, pass, warmup);

            Assert.Less(bytes, passes,
                $"{what}: {passes} passes after {warmup} of warm-up allocated {bytes} bytes ({(double)bytes / passes:0.#} a pass)");
        }
    }
}
