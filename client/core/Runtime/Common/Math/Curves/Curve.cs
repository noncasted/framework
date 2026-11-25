using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class Curve : ICurveDefinition
    {
        public Curve()
        {
        }

        public Curve(float time, AnimationCurve curve)
        {
            _time = time;
            _curve = curve;
        }

        [SerializeField] [Min(0f)] private float _time;
        [SerializeField] [CurveRange] private AnimationCurve _curve;

        public float Time => _time;
        public AnimationCurve Animation => _curve;

        public CurveInstance CreateInstance()
        {
            return new CurveInstance(this);
        }
    }

    public static class CurveExtensions
    {
        public static float Evaluate(this ICurveDefinition curve, float progress)
        {
            return curve.Animation.Evaluate(progress);
        }
    }
}