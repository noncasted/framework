using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    /// <summary>
    /// Read-only view of live containers. Data comes only from
    /// <see cref="ContainerRegistryDebug"/> / <see cref="IContainerDiagnostics"/>.
    /// </summary>
    public class ContainerDebuggerWindow : EditorWindow
    {
        private static readonly ILog _log = Logs.Of<ContainerDebuggerWindow>();

        private static readonly string UssPath = FrameworkSettings.Package("Editor/Tools/Container/ContainerDebuggerWindow.uss");

        private readonly ContainerDebuggerSelection _selection = new();

        private Lifetime _lifetime;

        private VisualElement _root;
        private Label _statusLabel;
        private ContainerTreeSection _tree;
        private ContainerClassAllocationsSection _classAllocations;
        private ContainerDetailsPane _details;

        [MenuItem(FrameworkMenu.Container + "Debugger", priority = FrameworkMenu.GroupPriority)]
        public static void Open()
        {
            var window = GetWindow<ContainerDebuggerWindow>();
            window.titleContent = new GUIContent("Container Debugger");
            window.minSize = new Vector2(800f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _lifetime?.Terminate();
            _lifetime = new Lifetime();

            try
            {
                ContainerRegistryDebug.Changed.Advise(_lifetime, Refresh);
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
            }

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _lifetime?.Terminate();
            _lifetime = null;
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();

            _root = new VisualElement();
            _root.AddToClassList("container-debugger-root");
            _root.style.flexGrow = 1;
            rootVisualElement.Add(_root);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);

            BuildHeader();
            BuildToolbar();
            BuildBody();
            BuildFooter();
            Refresh();
        }

        private void OnPlayModeChanged(PlayModeStateChange _)
        {
            EditorApplication.delayCall += Refresh;
        }

        private void Refresh()
        {
            if (this == null || _root == null || _selection.IsRefreshing)
                return;

            _selection.BeginRefresh();

            try
            {
                _tree.Refresh();
                _classAllocations.Refresh();
                SetStatus(ContainerDebuggerFormat.Status(_tree.RootCount, _tree.Containers), false);
                _details.Rebuild();
            }
            catch (Exception exception)
            {
                _log.Exception(exception);
                SetStatus("Failed to read container diagnostics", true);
            }
            finally
            {
                _selection.EndRefresh();
            }
        }

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("header");

            var title = new Label("Container Debugger");
            title.AddToClassList("header-title");
            header.Add(title);

            _root.Add(header);
        }

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            var refresh = new Button(Refresh) { text = "Refresh" };
            refresh.AddToClassList("toolbar-button");
            toolbar.Add(refresh);

            _root.Add(toolbar);
        }

        private void BuildBody()
        {
            _details = new ContainerDetailsPane(_selection);
            _tree = new ContainerTreeSection(_selection, _details.Rebuild);
            _classAllocations = new ContainerClassAllocationsSection();

            var body = new TwoPaneSplitView(0, 260f, TwoPaneSplitViewOrientation.Horizontal)
            {
                viewDataKey = "container-debugger-split"
            };
            body.AddToClassList("body");

            var treePane = new VisualElement();
            treePane.AddToClassList("tree-pane");
            _tree.AddTo(treePane);
            treePane.Add(_classAllocations.Build());

            body.Add(treePane);
            body.Add(_details.Build());

            _root.Add(body);
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("footer");

            _statusLabel = new Label();
            _statusLabel.AddToClassList("status-label");
            footer.Add(_statusLabel);

            _root.Add(footer);
        }

        private void SetStatus(string message, bool isError)
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text = message;
            _statusLabel.EnableInClassList("status-label--error", isError);
        }
    }
}
