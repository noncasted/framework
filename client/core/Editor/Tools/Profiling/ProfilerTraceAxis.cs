using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    // The header of the waterfall: the title of the name column and the time ticks.
    internal sealed class ProfilerTraceAxis
    {
        public VisualElement Element { get; } = new()
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                height = 22f,
                borderBottomWidth = 1f,
                borderBottomColor = new Color(0.3f, 0.31f, 0.34f)
            }
        };

        public void Rebuild(ProfilerTraceData trace)
        {
            Element.Clear();

            var title = new Label("Scope")
            {
                style =
                {
                    width = ProfilerTraceTimeline.NameColumnWidth,
                    flexShrink = 0f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.78f, 0.8f, 0.84f),
                    paddingLeft = 8f,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            Element.Add(title);

            var timeline = new VisualElement { style = { flexGrow = 1f } };
            Element.Add(timeline);

            var duration = trace?.DurationMs ?? 0d;

            for (var i = 0; i <= ProfilerTraceTimeline.TicksCount; i++)
            {
                var fraction = i / ProfilerTraceTimeline.TicksCount;

                var label = new Label(ProfilerTraceFormat.Duration(duration * fraction))
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = Length.Percent(fraction * 100f),
                        color = new Color(0.6f, 0.62f, 0.66f),
                        fontSize = 10f,
                        marginLeft = i == ProfilerTraceTimeline.TicksCount ? -44f : 2f,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        top = 4f
                    }
                };

                timeline.Add(label);
                timeline.Add(ProfilerTraceTimeline.CreateGridLine(fraction, 0.24f));
            }
        }
    }
}
