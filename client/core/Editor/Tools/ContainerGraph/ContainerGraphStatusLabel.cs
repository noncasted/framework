using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    [GraphToolbarElement(Id, typeof(ContainerLiveGraph), order: 50)]
    public sealed class ContainerGraphStatusLabel : Label, IAccessContainerWindow
    {
        public const string Id = "Internal.ContainerLiveGraph.Status";

        private EditorWindow _containerWindow;

        // Unity assigns it through the interface when it creates the toolbar element.
        EditorWindow IAccessContainerWindow.containerWindow
        {
            get => _containerWindow;
            set => _containerWindow = value;
        }

        public ContainerGraphStatusLabel()
        {
            name = "container-graph-status";
            text = ContainerLiveGraph.EmptyEditModeStatus;
            tooltip = "Live container tree status";
            style.unityTextAlign = TextAnchor.MiddleLeft;
            style.flexGrow = 1;
            style.minWidth = 280;
            style.marginLeft = 8;
            style.fontSize = 11;
            schedule.Execute(Refresh).Every(250);
        }

        private void Refresh()
        {
            if (_containerWindow is IGraphWindow graphWindow && graphWindow.Graph is ContainerLiveGraph graph)
            {
                text = string.IsNullOrEmpty(graph.Status)
                    ? ContainerLiveGraph.EmptyEditModeStatus
                    : graph.Status;
                return;
            }

            text = EditorApplication.isPlaying
                ? ContainerLiveGraph.EmptyPlayModeStatus
                : ContainerLiveGraph.EmptyEditModeStatus;
        }
    }
}
