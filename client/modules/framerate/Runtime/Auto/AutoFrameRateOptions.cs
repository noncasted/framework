using System;
using UnityEngine;

namespace Global.FrameRate
{
    // The numbers of the automatic frame rate (AutoFrameRateRule) and of the fixed choices of the settings. The work of a
    // frame is the time of the player loop without the wait for the screen (FrameWorkMeter): a 60 Hz frame has 16.7 ms, so
    // above DropWorkMs the phone heats up and iOS throttles it in waves; at the low rate a frame below RiseWorkMs leaves
    // room for the high one. The gap between the two keeps the limit from flipping.
    [Serializable]
    public class AutoFrameRateOptions
    {
        [SerializeField] [Min(1)] private int _high = 60;
        [SerializeField] [Min(1)] private int _low = 30;
        [SerializeField] [Min(0.1f)] private float _windowSeconds = 1f;
        [SerializeField] [Min(0f)] private float _dropWorkMs = 12f;
        [SerializeField] [Min(0f)] private float _dropSeconds = 8f;
        [SerializeField] [Min(0f)] private float _riseWorkMs = 6f;
        [SerializeField] [Min(0f)] private float _riseSeconds = 30f;

        public int High => _high;
        public int Low => _low;

        // The average of the work is taken over a window of this length; a frame later than a window after the previous
        // one (a hidden tab, a load) starts the count anew.
        public float WindowSeconds => _windowSeconds;

        // At the high rate: the average work above DropWorkMs in every window for DropSeconds drops to the low rate.
        public float DropWorkMs => _dropWorkMs;
        public float DropSeconds => _dropSeconds;

        // At the low rate: the average work below RiseWorkMs in every window for RiseSeconds rises to the high rate.
        public float RiseWorkMs => _riseWorkMs;
        public float RiseSeconds => _riseSeconds;

        public void Validate()
        {
            if (_low >= _high)
                throw new InvalidOperationException($"[FrameRate] The low rate {_low} must be below the high one {_high}");

            if (_riseWorkMs >= _dropWorkMs)
                throw new InvalidOperationException(
                    $"[FrameRate] The work to rise {_riseWorkMs} ms must be below the work to drop {_dropWorkMs} ms");

            if (_windowSeconds <= 0f)
                throw new InvalidOperationException($"[FrameRate] The window {_windowSeconds} s must be positive");
        }
    }
}
