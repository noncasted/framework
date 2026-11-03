using Internal;

namespace Global.Telemetry
{
    // The time of the CPU, of its main thread and of the GPU, averaged over the sample. Where nothing comes in the first
    // frames the collector stops listening; a part that never comes never gets a name.
    public class FrameTimingsTelemetryCollector : ITelemetryCollector, ITelemetryLiveCollector, ILateUpdatable
    {
        public FrameTimingsTelemetryCollector(IUpdater updater, ITelemetryFrameTimings timings)
        {
            _updater = updater;
            _timings = timings;
        }

        public const int ProbeFrames = 120;

        private readonly IUpdater _updater;
        private readonly ITelemetryFrameTimings _timings;
        private readonly FrameTimingAverage _cpu = new();
        private readonly FrameTimingAverage _cpuMain = new();
        private readonly FrameTimingAverage _gpu = new();

        private ILifetime _listening;
        private int _probeFramesLeft = ProbeFrames;

        public void Start(IReadOnlyLifetime running)
        {
            _cpu.Clear();
            _cpuMain.Clear();
            _gpu.Clear();
            _probeFramesLeft = ProbeFrames;
            _listening = running.Child();
            _updater.Add(_listening, this);
        }

        public void OnLateUpdate(float delta)
        {
            var timing = _timings.Read();

            _cpu.Add(timing.CpuMs);
            _cpuMain.Add(timing.CpuMainMs);
            _gpu.Add(timing.GpuMs);

            if (_probeFramesLeft > 0)
                Probe(timing);
        }

        public void Collect(ITelemetrySample sample)
        {
            if (_cpu.TryTake(out var cpu))
                sample.Gauge("frame.cpu", cpu, TelemetryUnits.Milliseconds);

            if (_cpuMain.TryTake(out var cpuMain))
                sample.Gauge("frame.cpu.main", cpuMain, TelemetryUnits.Milliseconds);

            if (_gpu.TryTake(out var gpu))
                sample.Gauge("frame.gpu", gpu, TelemetryUnits.Milliseconds);
        }

        private void Probe(TelemetryFrameTiming timing)
        {
            if (timing.CpuMs > 0d || timing.CpuMainMs > 0d || timing.GpuMs > 0d)
            {
                _probeFramesLeft = 0;
                return;
            }

            _probeFramesLeft--;

            if (_probeFramesLeft == 0)
                _listening.Terminate();
        }
    }
}
