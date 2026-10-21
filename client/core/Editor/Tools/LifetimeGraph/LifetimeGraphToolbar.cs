using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    [GraphToolbarElement(Id, typeof(LifetimeLiveGraph), order: 40)]
    public sealed class LifetimeGraphClearButton : EditorToolbarButton, IAccessContainerWindow
    {
        public const string Id = "Internal.LifetimeLiveGraph.Clear";

        public EditorWindow containerWindow { get; set; }

        public LifetimeGraphClearButton()
        {
            text = "Clear";
            tooltip = "Forget all tracked lifetimes; only lifetimes created from now on are shown";
            RegisterCallback<ClickEvent>(_ => ClearTracked());
        }

        private void ClearTracked()
        {
            if (containerWindow is IGraphWindow { Graph: LifetimeLiveGraph graph })
                graph.ClearTracked();
        }
    }

    [GraphToolbarElement(Id, typeof(LifetimeLiveGraph), order: 50)]
    public sealed class LifetimeGraphStatusLabel : Label, IAccessContainerWindow
    {
        public const string Id = "Internal.LifetimeLiveGraph.Status";

        public EditorWindow containerWindow { get; set; }

        public LifetimeGraphStatusLabel()
        {
            tooltip = "Alive lifetimes. Red nodes outlived their parent.";
            style.unityTextAlign = TextAnchor.MiddleLeft;
            style.flexGrow = 1;
            style.minWidth = 280;
            style.marginLeft = 8;
            style.fontSize = 11;
            schedule.Execute(Refresh).Every(250);
        }

        private void Refresh()
        {
            if (containerWindow is IGraphWindow { Graph: LifetimeLiveGraph graph })
                text = graph.Status;
        }
    }
}
