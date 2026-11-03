using System;

namespace Global.Telemetry.Tests
{
    public class ThrowingFrameListener : IFramePhaseListener
    {
        public void OnFrameStarted()
        {
            throw new InvalidOperationException("listener");
        }

        public void OnFrameEnded(FramePhaseFrame frame)
        {
        }
    }
}
