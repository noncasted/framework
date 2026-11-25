using UnityEngine;

namespace Internal
{
    public interface ICurveDefinition
    {
        float Time { get; }
        AnimationCurve Animation { get; }

        CurveInstance CreateInstance();
    }
}
