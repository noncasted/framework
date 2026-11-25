using UnityEngine;

namespace Internal
{
    public struct CurveInstance
    {
        public CurveInstance(ICurveDefinition curve)
        {
            Curve = curve;
            _timer = 0f;
            _progress = 0f;
        }

        private float _timer;
        private float _progress;

        public readonly ICurveDefinition Curve;

        public bool IsFinished => Mathf.Approximately(_progress, 1f);
        public float Progress => _progress;

        public float StepForward(float delta)
        {
            _timer += delta;
            _timer = Mathf.Clamp(_timer, 0f, Curve.Time);

            // A curve of zero time is a jump: forward is its end.
            _progress = Curve.Time > 0f ? Mathf.Min(_timer / Curve.Time, 1f) : 1f;

            return Curve.Evaluate(_progress);
        }

        public float StepBack(float delta)
        {
            _timer -= delta;
            _timer = Mathf.Clamp(_timer, 0f, Curve.Time);

            // A curve of zero time is a jump: back is its start.
            _progress = Curve.Time > 0f ? Mathf.Min(_timer / Curve.Time, 1f) : 0f;

            return Curve.Evaluate(_progress);
        }
    }
}
