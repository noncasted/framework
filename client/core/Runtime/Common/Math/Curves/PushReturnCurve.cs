using System;
using UnityEngine;

namespace Internal
{
    // Push and return as a single curve so both are edited in one window: X 0..1 is push progress,
    // 1..2 is return progress. Values share the same units; the return goes from the peak at PushEnd down to zero.
    [Serializable]
    public class PushReturnCurve
    {
        public PushReturnCurve()
            : this(1f)
        {
        }

        public PushReturnCurve(float peak)
        {
            _curve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(PushEnd, peak, 0f, 0f),
                new Keyframe(ReturnEnd, 0f, 0f, 0f));
        }

        public const float PushEnd = 1f;
        public const float ReturnEnd = 2f;

        [SerializeField] private AnimationCurve _curve;

        public AnimationCurve Curve => _curve;

        // push, back - phase progress 0..1. The return starts once the push is finished: while back == 0,
        // the push is in progress.
        public float Evaluate(float push, float back)
        {
            if (back > 0f)
                return _curve.Evaluate(PushEnd + back * (ReturnEnd - PushEnd));

            return _curve.Evaluate(push * PushEnd);
        }
    }
}
