using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Internal
{
    [GraphToolbarElement(Id, typeof(ContainerLiveGraph), order: 40)]
    public sealed class ContainerGraphRefreshButton : EditorToolbarButton, IAccessContainerWindow
    {
        public const string Id = "Internal.ContainerLiveGraph.Refresh";

        private EditorWindow _containerWindow;

        // Unity assigns it through the interface when it creates the toolbar element.
        EditorWindow IAccessContainerWindow.containerWindow
        {
            get => _containerWindow;
            set => _containerWindow = value;
        }

        public ContainerGraphRefreshButton()
        {
            text = "Refresh";
            tooltip = "Rebuild nodes from live ContainerRegistryDebug.Roots";
            RegisterCallback<ClickEvent>(_ => Rebuild());
        }

        private void Rebuild()
        {
            if (_containerWindow is IGraphWindow graphWindow && graphWindow.Graph is ContainerLiveGraph graph)
                graph.Rebuild();
        }
    }
}