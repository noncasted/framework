using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Internal
{
    [GraphToolbarElement(Id, typeof(LifetimeLiveGraph), order: 40)]
    public sealed class LifetimeGraphClearButton : EditorToolbarButton, IAccessContainerWindow
    {
        public const string Id = "Internal.LifetimeLiveGraph.Clear";

        private EditorWindow _containerWindow;

        // Unity assigns it through the interface when it creates the toolbar element.
        EditorWindow IAccessContainerWindow.containerWindow
        {
            get => _containerWindow;
            set => _containerWindow = value;
        }

        public LifetimeGraphClearButton()
        {
            text = "Clear";
            tooltip = "Forget all tracked lifetimes; only lifetimes created from now on are shown";
            RegisterCallback<ClickEvent>(_ => ClearTracked());
        }

        private void ClearTracked()
        {
            if (_containerWindow is IGraphWindow { Graph: LifetimeLiveGraph graph })
                graph.ClearTracked();
        }
    }
}
