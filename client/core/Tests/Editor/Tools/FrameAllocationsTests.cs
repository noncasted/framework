using NUnit.Framework;

namespace Internal.Tests
{
    // The guard counts what a pass allocates (the counter has read 0 in Unity Mono before: a guard on a dead counter
    // passes everything) and leaves the warm-up out.
    [Category("Tools")]
    public class FrameAllocationsTests
    {
        private const int Passes = 100;

        private object _sink;

        [Test]
        public void Measure_ArrayOfKnownSize_CountsItEveryPass()
        {
            var bytes = FrameAllocations.Measure(Passes, () => _sink = new byte[1024]);

            Assert.GreaterOrEqual(bytes, Passes * 1024L);
            Assert.NotNull(_sink);
        }

        [Test]
        public void Measure_AllocationOfTheWarmup_NotCounted()
        {
            var isWarm = 0;

            var bytes = FrameAllocations.Measure(Passes, () => {
                if (isWarm++ < FrameAllocations.DefaultWarmup)
                    _sink = new byte[1024];
            });

            Assert.Less(bytes, Passes);
        }

        [Test]
        public void AssertNone_ObjectEveryPass_FailsWithTheBytes()
        {
            var exception = Assert.Throws<AssertionException>(
                () => FrameAllocations.AssertNone("the step", Passes, () => _sink = new object()));

            StringAssert.Contains($"the step: {Passes} passes after {FrameAllocations.DefaultWarmup} of warm-up allocated",
                exception.Message);
        }

        [Test]
        public void AssertNone_PassWithoutAllocation_Passes()
        {
            var sum = 0;

            FrameAllocations.AssertNone("the sum", Passes, () => sum++);

            Assert.AreEqual(FrameAllocations.DefaultWarmup + Passes, sum);
        }
    }
}
