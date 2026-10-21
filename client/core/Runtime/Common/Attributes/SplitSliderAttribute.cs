using System;
using UnityEngine;

namespace Internal
{
    // Splits the 0..1 range into three parts: the Vector2 field stores the end of the first (x) and the end of the second (y),
    // the third is the remainder up to 1. If TotalProperty is set (a float at the root of the same object), the parts
    // are also labelled in seconds derived from it.
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class SplitSliderAttribute : PropertyAttribute
    {
        public SplitSliderAttribute(string first, string second, string third, string totalProperty = null)
        {
            Names = new[] { first, second, third };
            TotalProperty = totalProperty;
        }

        public string[] Names { get; }
        public string TotalProperty { get; }
    }
}
