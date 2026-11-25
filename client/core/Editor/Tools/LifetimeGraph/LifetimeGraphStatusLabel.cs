using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    [GraphToolbarElement(Id, typeof(LifetimeLiveGraph), order: 50)]
    public sealed class LifetimeGraphStatusLabel : Label, IAccessContainerWindow
    {
        public const string Id = "Internal.LifetimeLiveGraph.Status";

        private EditorWindow _containerWindow;

        // Unity assigns it through the interface when it creates the toolbar element.
        EditorWindow IAccessContainerWindow.containerWindow
        {
            get => _containerWindow;
            set => _containerWindow = value;
        }

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
            if (_containerWindow is IGraphWindow { Graph: LifetimeLiveGraph graph })
                text = graph.Status;
        }
    }
}
