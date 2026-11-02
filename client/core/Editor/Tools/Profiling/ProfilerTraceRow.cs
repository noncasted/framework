using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    // One span of the waterfall: its name in the tree and its bar on the timeline.
    internal static class ProfilerTraceRow
    {
        public const float FullHeight = 22f;

        private const float ValueFontSize = 10f;

        /// <summary>Small allocations only add noise to the bar label: the full number is always in the tooltip.</summary>
        private const long MinShownAllocBytes = 64L * 1024L;

        private static readonly Color[] _barColors =
        {
            new(0.29f, 0.72f, 0.71f),
            new(0.36f, 0.63f, 0.80f),
            new(0.55f, 0.68f, 0.45f),
            new(0.85f, 0.66f, 0.38f),
            new(0.72f, 0.52f, 0.75f),
            new(0.83f, 0.47f, 0.47f)
        };

        public static VisualElement Create(
            ProfilerSpanData span,
            bool hasChildren,
            bool isCollapsed,
            int index,
            float rowHeight,
            double traceDurationMs,
            Action onToggle)
        {
            var duration = Math.Max(traceDurationMs, 0.001d);
            var scale = rowHeight / FullHeight;
            var barHeight = Mathf.Max(4f, Mathf.Round(10f * scale));
            var barTop = Mathf.Round((rowHeight - barHeight) * 0.5f);
            var labelTop = Mathf.Round((rowHeight - ValueFontSize * 1.4f) * 0.5f);

            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    height = rowHeight,
                    backgroundColor = index % 2 == 0
                        ? new Color(0f, 0f, 0f, 0f)
                        : new Color(1f, 1f, 1f, 0.025f)
                }
            };

            var name = new VisualElement
            {
                style =
                {
                    width = ProfilerTraceTimeline.NameColumnWidth,
                    flexShrink = 0f,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    overflow = Overflow.Hidden,
                    paddingLeft = 8f + span.Depth * Mathf.Round(14f * scale)
                }
            };

            if (hasChildren)
            {
                var arrow = new Label(isCollapsed ? "▶" : "▼")
                {
                    style =
                    {
                        width = 14f,
                        fontSize = 8f,
                        color = new Color(0.65f, 0.67f, 0.7f),
                        unityTextAlign = TextAnchor.MiddleLeft
                    }
                };

                name.Add(arrow);

                // Click on the whole name row, not only the arrow: hitting 14 pixels
                // at deep nesting is inconvenient.
                name.RegisterCallback<MouseDownEvent>(_ => onToggle());
            }
            else
            {
                name.Add(new VisualElement { style = { width = 14f } });
            }

            name.Add(new VisualElement
            {
                style =
                {
                    width = 3f,
                    height = Mathf.Max(6f, Mathf.Round(14f * scale)),
                    marginRight = 6f,
                    backgroundColor = ColorFor(span.Depth)
                }
            });

            name.Add(new Label(span.Name)
            {
                tooltip = span.Name,
                style =
                {
                    color = new Color(0.86f, 0.88f, 0.9f),
                    overflow = Overflow.Hidden,
                    whiteSpace = WhiteSpace.NoWrap,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    flexGrow = 1f
                }
            });

            row.Add(name);

            var timeline = new VisualElement { style = { flexGrow = 1f } };

            for (var i = 0; i <= ProfilerTraceTimeline.TicksCount; i++)
                timeline.Add(ProfilerTraceTimeline.CreateGridLine(i / ProfilerTraceTimeline.TicksCount, 0.06f));

            var left = (float)(span.StartMs / duration * 100d);
            var width = Mathf.Max(0.15f, (float)(span.DurationMs / duration * 100d));

            var bar = new VisualElement
            {
                tooltip =
                    $"{span.Name}\nstart {ProfilerTraceFormat.Duration(span.StartMs)}\nduration {ProfilerTraceFormat.Duration(span.DurationMs)}\nframes {span.Frames}\nalloc {ProfilerTraceFormat.Bytes(span.AllocBytes)}",
                style =
                {
                    position = Position.Absolute,
                    left = Length.Percent(left),
                    width = Length.Percent(width),
                    top = barTop,
                    height = barHeight,
                    backgroundColor = ColorFor(span.Depth),
                    borderTopLeftRadius = 2f,
                    borderTopRightRadius = 2f,
                    borderBottomLeftRadius = 2f,
                    borderBottomRightRadius = 2f
                }
            };

            timeline.Add(bar);

            // A long span hits the right edge, so its duration is written inside the bar.
            var isWide = left + width > 82f;

            // Frames next to the duration: a span that waited for frames and a span
            // that burned CPU look the same on the timeline, but are fixed differently.
            var text = ProfilerTraceFormat.Duration(span.DurationMs) +
                       (span.Frames > 0 ? $"  ({span.Frames} fr)" : string.Empty) +
                       (span.AllocBytes >= MinShownAllocBytes ? $"  {ProfilerTraceFormat.Bytes(span.AllocBytes)}" : string.Empty) +
                       (span.Unfinished ? "  (unfinished)" : string.Empty);

            var value = new Label(text)
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = Length.Percent(isWide ? left : Mathf.Min(left + width, 99f)),
                    marginLeft = 6f,
                    top = labelTop,
                    fontSize = ValueFontSize,
                    color = isWide
                        ? new Color(0.1f, 0.12f, 0.14f)
                        : new Color(0.68f, 0.7f, 0.74f)
                }
            };

            timeline.Add(value);
            row.Add(timeline);

            return row;
        }

        private static Color ColorFor(int depth)
        {
            return _barColors[Mathf.Clamp(depth, 0, _barColors.Length - 1)];
        }
    }
}
