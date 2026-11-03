using UnityEngine;

namespace Global.Telemetry
{
    // A player gives values only with Player Settings / Frame Timing Stats on, and the GPU only where the browser has
    // timer queries.
    public class UnityTelemetryFrameTimings : ITelemetryFrameTimings
    {
        private readonly FrameTiming[] _latest = new FrameTiming[1];

        public TelemetryFrameTiming Read()
        {
            FrameTimingManager.CaptureFrameTimings();

            if (FrameTimingManager.GetLatestTimings(1, _latest) == 0)
                return default;

            var timing = _latest[0];

            return new TelemetryFrameTiming(timing.cpuFrameTime, timing.cpuMainThreadFrameTime, timing.gpuFrameTime);
        }
    }
}
