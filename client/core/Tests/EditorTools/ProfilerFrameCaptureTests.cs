using NUnit.Framework;

namespace Internal.EditorTests
{
    public class ProfilerFrameCaptureTests
    {
        // A trace without the profiler clock, in Edit Mode: there is no offset to find its frames by.
        [Test]
        public void TraceWithoutClock_ReportsThatTheProfilerDidNotRecord()
        {
            var trace = new ProfilerTraceData { Name = "Run", Id = "2026", StartFrame = 5, EndFrame = 9 };

            var frames = ProfilerFrameCapture.Capture(trace);

            Assert.AreEqual("Run", frames.TraceName);
            Assert.AreEqual("2026", frames.TraceId);
            Assert.AreEqual(5, frames.FirstFrame);
            Assert.AreEqual(9, frames.LastFrame);
            Assert.AreEqual("Main Thread", frames.ThreadName);
            Assert.AreEqual(0.1d, frames.MinSampleMs);
            Assert.IsEmpty(frames.Frames);
            Assert.AreEqual("Профайлер не писал кадры этого запуска: включи запись в окне Profiler.", frames.Warning);
        }
    }
}
