using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    // The body of the waterfall: the spans of the shown trace, without the collapsed subtrees, fitted to the window height.
    internal sealed class ProfilerTraceRows
    {
        public ProfilerTraceRows(Label summary)
        {
            _summary = summary;

            // The row height is derived from the list height, so the list is rebuilt on every
            // window resize.
            Element.RegisterCallback<GeometryChangedEvent>(evt => {
                if (Mathf.Abs(evt.newRect.height - evt.oldRect.height) < 1f)
                    return;

                Rebuild();
            });
        }

        private const float MinRowHeight = 14f;

        private readonly Label _summary;
        private readonly HashSet<int> _collapsed = new();

        private ProfilerTraceData _trace;

        public ScrollView Element { get; } = new(ScrollViewMode.Vertical)
        {
            style = { flexGrow = 1f }
        };

        public void Show(ProfilerTraceData trace)
        {
            _trace = trace;
            CollapseSpans();
            Rebuild();
        }

        public void ExpandAll()
        {
            _collapsed.Clear();
            Rebuild();
        }

        public void CollapseAll()
        {
            CollapseSpans();
            Rebuild();
        }

        private void Rebuild()
        {
            Element.Clear();

            if (_trace == null || _trace.Spans.Count == 0)
            {
                _summary.text = "Трасс нет. Запусти игру — профайлер сохранит замер загрузки на выходе в меню.";
                return;
            }

            var depth = _trace.Spans.Max(span => span.Depth) + 1;

            _summary.text =
                $"{_trace.Name}    •    Start {_trace.StartedAt}    •    Duration {ProfilerTraceFormat.Duration(_trace.DurationMs)}" +
                $"    •    Frames {_trace.Frames}    •    Spans {_trace.Spans.Count}    •    Depth {depth}" +
                (_trace.Cold ? "    •    COLD (первый запуск после перекомпиляции)" : string.Empty);

            var children = new Dictionary<int, List<ProfilerSpanData>>();
            var byId = _trace.Spans.ToDictionary(span => span.Id);

            foreach (var span in _trace.Spans)
            {
                if (children.TryGetValue(span.ParentId, out var list) == false)
                {
                    list = new List<ProfilerSpanData>();
                    children.Add(span.ParentId, list);
                }

                list.Add(span);
            }

            var visible = new List<ProfilerSpanData>();

            foreach (var span in _trace.Spans)
            {
                if (IsVisible(span))
                    visible.Add(span);
            }

            var rowHeight = FitRowHeight(visible.Count);

            for (var index = 0; index < visible.Count; index++)
            {
                var span = visible[index];
                Element.Add(ProfilerTraceRow.Create(
                    span,
                    children.ContainsKey(span.Id),
                    _collapsed.Contains(span.Id),
                    index,
                    rowHeight,
                    _trace.DurationMs,
                    () => Toggle(span.Id)));
            }

            return;

            bool IsVisible(ProfilerSpanData span)
            {
                var current = span;

                while (current.ParentId >= 0 && byId.TryGetValue(current.ParentId, out var parent))
                {
                    current = parent;

                    if (_collapsed.Contains(current.Id))
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// An expanded trace is about a hundred rows, and in a vertical scroll the waterfall
        /// cannot be read: the overall picture of the run is lost. So rows shrink to fit
        /// the window height as long as it stays readable.
        /// </summary>
        private float FitRowHeight(int count)
        {
            if (count <= 0)
                return ProfilerTraceRow.FullHeight;

            var available = Element.resolvedStyle.height;

            // Before the first layout there is no height yet - then draw at full size,
            // and rebuild on GeometryChangedEvent.
            if (float.IsNaN(available) || available <= 1f)
                return ProfilerTraceRow.FullHeight;

            return Mathf.Clamp(Mathf.Floor(available / count), MinRowHeight, ProfilerTraceRow.FullHeight);
        }

        private void Toggle(int spanId)
        {
            if (_collapsed.Remove(spanId) == false)
                _collapsed.Add(spanId);

            Rebuild();
        }

        /// <summary>
        /// Default state: the trace opens collapsed, then it is expanded
        /// along the stages that turned out to be long.
        /// </summary>
        private void CollapseSpans()
        {
            _collapsed.Clear();

            if (_trace == null)
                return;

            foreach (var span in _trace.Spans)
            {
                if (span.Depth >= 1)
                    _collapsed.Add(span.Id);
            }
        }
    }
}
