using Unity.GraphToolkit.Editor;
using UnityEngine.UIElements;

namespace Internal
{
    public sealed class LifetimeNodeView : NodeView<LifetimeNode>
    {
        private Button _toggle;

        public override void OnViewBuilt()
        {
            _toggle = new Button(Toggle);
            _toggle.style.marginLeft = 6;
            _toggle.style.marginRight = 6;
            _toggle.style.marginBottom = 4;
            _toggle.style.fontSize = 10;
            View.Root.Add(_toggle);

            // The child counter changes on sync without rebuilding the view: the label is updated by a timer.
            _toggle.schedule.Execute(Refresh).Every(250);
            Refresh();
        }

        public override void OnCullingChanged(bool cullingEnabled)
        {
            if (cullingEnabled == false && _toggle != null)
                View.Root.Add(_toggle);
        }

        private void Refresh()
        {
            var count = Node.DescendantCount;

            _toggle.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _toggle.text = Node.IsCollapsed ? $"Expand ({count})" : $"Collapse ({count})";
        }

        private void Toggle()
        {
            var graph = (LifetimeLiveGraph)Node.Graph;
            graph.ToggleCollapsed(Node.LifetimeId);
        }
    }
}
