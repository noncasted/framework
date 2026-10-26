using System;
using UnityEngine;

namespace Internal
{
    public static class Easing
    {
        private const float BackOvershoot = 1.70158f;

        // progress is the linear share of the time, 0..1. OutBack goes beyond 1 on the way, so the result is passed
        // to LerpUnclamped.
        public static float Evaluate(EaseType ease, float progress)
        {
            switch (ease)
            {
                case EaseType.Linear:
                    return progress;
                case EaseType.OutQuad:
                    return progress * (2f - progress);
                case EaseType.InOutSine:
                    return -0.5f * (Mathf.Cos(Mathf.PI * progress) - 1f);
                case EaseType.OutCirc:
                {
                    var inverse = progress - 1f;
                    return Mathf.Sqrt(1f - inverse * inverse);
                }
                case EaseType.OutBack:
                {
                    var inverse = progress - 1f;
                    return inverse * inverse * ((BackOvershoot + 1f) * inverse + BackOvershoot) + 1f;
                }
                case EaseType.InBounce:
                    return 1f - OutBounce(1f - progress);
                default:
                    throw new ArgumentOutOfRangeException(nameof(ease), ease, null);
            }
        }

        private static float OutBounce(float progress)
        {
            const float strength = 7.5625f;
            const float period = 2.75f;

            if (progress < 1f / period)
                return strength * progress * progress;

            if (progress < 2f / period)
            {
                progress -= 1.5f / period;
                return strength * progress * progress + 0.75f;
            }

            if (progress < 2.5f / period)
            {
                progress -= 2.25f / period;
                return strength * progress * progress + 0.9375f;
            }

            progress -= 2.625f / period;
            return strength * progress * progress + 0.984375f;
        }
    }
}
