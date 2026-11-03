using Internal;
using UnityEngine.LowLevel;

namespace Global.Telemetry
{
    // Puts the markers of FramePhaseClock into the player loop for a session and takes them out at its end: outside a session the
    // loop is the one of the game. Stopwatch only, so it works where the profiler has no markers (a release WebGL).
    public class FramePhaseLoop : ITelemetryLiveCollector
    {
        public FramePhaseLoop(FramePhaseClock clock)
        {
            _clock = clock;
            _begins = new PlayerLoopSystem.UpdateFunction[FramePhases.Count];
            _ends = new PlayerLoopSystem.UpdateFunction[FramePhases.Count];

            for (var i = 0; i < FramePhases.Count; i++)
            {
                var phase = i;
                _begins[i] = () => _clock.Begin(phase);
                _ends[i] = () => _clock.End(phase);
            }
        }

        private readonly FramePhaseClock _clock;
        private readonly PlayerLoopSystem.UpdateFunction[] _begins;
        private readonly PlayerLoopSystem.UpdateFunction[] _ends;

        public void Start(IReadOnlyLifetime running)
        {
            _clock.Reset();
            PlayerLoop.SetPlayerLoop(FramePhaseLoopEdit.With(PlayerLoop.GetCurrentPlayerLoop(), _begins, _ends));
            running.Listen(Remove);
        }

        private static void Remove()
        {
            PlayerLoop.SetPlayerLoop(FramePhaseLoopEdit.Without(PlayerLoop.GetCurrentPlayerLoop()));
        }
    }
}
