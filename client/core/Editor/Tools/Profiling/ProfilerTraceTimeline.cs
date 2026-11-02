using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    // The columns shared by the axis and the rows of the waterfall: the name column and the grid of the timeline.
    internal static class ProfilerTraceTimeline
    {
        public const float NameColumnWidth = 380f;
        public const float TicksCount = 4f;

        public static VisualElement CreateGridLine(float fraction, float alpha)
        {
            return new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = Length.Percent(fraction * 100f),
                    top = 0f,
                    bottom = 0f,
                    width = 1f,
                    backgroundColor = new Color(1f, 1f, 1f, alpha)
                }
            };
        }
    }
}
