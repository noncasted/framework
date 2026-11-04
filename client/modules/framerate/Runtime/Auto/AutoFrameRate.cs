using Internal;
using UnityEngine;

namespace Global.FrameRate
{
    // Feeds AutoFrameRateRule with the work of each frame on the updater and remembers each decision for the next start.
    public class AutoFrameRate : IAutoFrameRate, IUpdatable
    {
        public AutoFrameRate(AutoFrameRateOptions options, IUpdater updater, FrameWorkMeter meter, AutoFrameRateMemory memory)
        {
            _options = options;
            _updater = updater;
            _meter = meter;
            _memory = memory;
        }

        private readonly AutoFrameRateOptions _options;
        private readonly IUpdater _updater;
        private readonly FrameWorkMeter _meter;
        private readonly AutoFrameRateMemory _memory;
        private readonly ViewableProperty<int> _target = new();
        private readonly EventSource<int> _switched = new();

        private AutoFrameRateRule _rule;

        public IViewableProperty<int> Target => _target;
        public IEventSource<int> Switched => _switched;

        public void Run(IReadOnlyLifetime lifetime)
        {
            _rule = new AutoFrameRateRule(_options, _memory.IsLow);
            _target.Set(_rule.Target);
            _meter.Run(lifetime);
            _updater.Add(lifetime, this);
        }

        // The work of the previous frame: the meter closes a frame after every update of it.
        public void OnUpdate(float delta)
        {
            if (_rule.Feed(Time.realtimeSinceStartupAsDouble, _meter.LastWorkMs) == false)
                return;

            _memory.Remember(_rule.IsLow);
            Debug.Log($"[FrameRate] Auto: {_rule.Target} frames per second, the last frame took {_meter.LastWorkMs:0.0} ms");
            _target.Set(_rule.Target);
            _switched.Invoke(_rule.Target);
        }
    }
}
