using System;
using UnityEngine;

namespace Internal
{
    // Range of push values for PushReturnCurve in the inspector; without it the range is 0..1.
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class PushReturnRangeAttribute : PropertyAttribute
    {
        public PushReturnRangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Min { get; }
        public float Max { get; }
    }
}
