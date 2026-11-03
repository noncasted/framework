using System;
using System.Collections.Generic;

namespace Global.Telemetry
{
    // The markers of FramePhaseLoop end here: the time of each phase of a frame, handed to the listeners at its end. A frame
    // counts only from its first phase on, so a session that starts in the middle of a frame skips that one. A listener that
    // throws is left out until the next session: it would throw every frame.
    public class FramePhaseClock
    {
        public FramePhaseClock(ITelemetryTicks ticks, IReadOnlyList<IFramePhaseListener> listeners)
        {
            _ticks = ticks;
            _listeners = listeners;
            _active = new IFramePhaseListener[listeners.Count];
        }

        private readonly ITelemetryTicks _ticks;
        private readonly IReadOnlyList<IFramePhaseListener> _listeners;
        private readonly IFramePhaseListener[] _active;
        private readonly double[] _started = new double[FramePhases.Count];
        private readonly FramePhaseFrame _frame = new();

        private bool _isInFrame;
        private double _lastEnd;

        public void Reset()
        {
            _isInFrame = false;
            _lastEnd = 0d;
            _frame.Clear();

            for (var i = 0; i < _active.Length; i++)
                _active[i] = _listeners[i];
        }

        public void Begin(int phase)
        {
            var now = _ticks.NowMs;

            if (phase == FramePhases.Early)
                StartFrame();

            if (_isInFrame == false)
                return;

            _started[phase] = now;

            if (phase == FramePhases.Fixed)
                _frame.AddFixedStep();
        }

        public void End(int phase)
        {
            if (_isInFrame == false)
                return;

            var now = _ticks.NowMs;
            _frame.AddSpent(phase, now - _started[phase]);

            if (phase == FramePhases.PostLate)
                EndFrame(now);
        }

        private void StartFrame()
        {
            _isInFrame = true;
            _frame.Clear();

            for (var i = 0; i < _active.Length; i++)
                Notify(i, null);
        }

        private void EndFrame(double now)
        {
            _isInFrame = false;
            _frame.SetInterval(_lastEnd > 0d ? now - _lastEnd : 0d);
            _lastEnd = now;

            for (var i = 0; i < _active.Length; i++)
                Notify(i, _frame);
        }

        // The isolation of subscribers inside the player loop.
        private void Notify(int index, FramePhaseFrame ended)
        {
            var listener = _active[index];

            if (listener == null)
                return;

            try
            {
                if (ended == null)
                    listener.OnFrameStarted();
                else
                    listener.OnFrameEnded(ended);
            }
            catch (Exception exception)
            {
                _active[index] = null;
                TelemetrySelfLog.Exception(exception);
            }
        }
    }
}
