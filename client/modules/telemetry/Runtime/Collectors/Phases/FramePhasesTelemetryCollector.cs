using Internal;

namespace Global.Telemetry
{
    // frame.phase.<phase> and .max, frame.fixed_steps and .max: per frame, averaged over the sample. The sum of the phases is close
    // to frame.cpu.main where the engine reports it.
    public class FramePhasesTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector, IFramePhaseListener
    {
        public FramePhasesTelemetryCollector()
        {
            for (var i = 0; i < FramePhases.Count; i++)
            {
                _phases[i] = new FramePhaseStat();
                _averageNames[i] = "frame.phase." + FramePhases.Names[i];
                _maxNames[i] = _averageNames[i] + ".max";
            }
        }

        private readonly FramePhaseStat[] _phases = new FramePhaseStat[FramePhases.Count];
        private readonly string[] _averageNames = new string[FramePhases.Count];
        private readonly string[] _maxNames = new string[FramePhases.Count];
        private readonly FramePhaseStat _fixedSteps = new();

        public void Start(IReadOnlyLifetime running)
        {
            foreach (var phase in _phases)
                phase.Clear();

            _fixedSteps.Clear();
        }

        public void OnFrameStarted()
        {
        }

        public void OnFrameEnded(FramePhaseFrame frame)
        {
            for (var i = 0; i < FramePhases.Count; i++)
                _phases[i].Add(frame.SpentMs(i));

            _fixedSteps.Add(frame.FixedSteps);
        }

        public void Collect(ITelemetrySample sample)
        {
            for (var i = 0; i < FramePhases.Count; i++)
            {
                if (_phases[i].TryTake(out var average, out var max) == false)
                    continue;

                sample.Gauge(_averageNames[i], average, TelemetryUnits.Milliseconds);
                sample.Gauge(_maxNames[i], max, TelemetryUnits.Milliseconds);
            }

            if (_fixedSteps.TryTake(out var steps, out var maxSteps))
            {
                sample.Gauge("frame.fixed_steps", steps, TelemetryUnits.Count);
                sample.Gauge("frame.fixed_steps.max", maxSteps, TelemetryUnits.Count);
            }
        }
    }
}
