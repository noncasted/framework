using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    // The tabs are the ProjectToolsTab classes marked with [ProjectToolsTab] in every editor assembly: the framework
    // ones and the tabs of the project. A tab is built when it is shown first, so the window opens at once and a heavy
    // tab pays only when selected. The selected tab is restored on the next opening.
    public class ProjectToolsWindow : EditorWindow, IProjectToolsHost
    {
        private const string ActiveTabPrefsKey = "Internal.ProjectTools.ActiveTab";

        private static readonly string UssPath = FrameworkSettings.Package("Editor/Tools/ProjectTools/ProjectToolsWindow.uss");
        private static readonly ILog _log = Logs.Of<ProjectToolsWindow>();

        // A content is null until its tab is shown.
        private readonly List<VisualElement> _tabContents = new();
        private readonly List<Button> _tabButtons = new();

        private List<ProjectToolsTab> _tabs;
        private OptionsContainer _options;
        private VisualElement _root;
        private ScrollView _scrollHost;
        private VisualElement _fillHost;
        private Label _statusLabel;

        public OptionsContainer Options => _options;

        // Ctrl+G (Cmd+G) by default. A project that needs the key for a tool of its own defines
        // FRAMEWORK_NO_PROJECT_TOOLS_HOTKEY (scripting define symbols of every platform it builds) and binds
        // ToggleWindow to another key in its editor assembly, e.g. [Shortcut("Project Tools", KeyCode.T, ShortcutModifiers.Action)].
#if FRAMEWORK_NO_PROJECT_TOOLS_HOTKEY
        [MenuItem(FrameworkMenu.Root + "Project Tools", priority = FrameworkMenu.Priority)]
#else
        [MenuItem(FrameworkMenu.Root + "Project Tools %g", priority = FrameworkMenu.Priority)]
#endif
        public static void ToggleWindow()
        {
            // Only a focused window is closed: one open behind a dock tab or another window comes forward instead.
            if (focusedWindow is ProjectToolsWindow opened)
            {
                opened.Close();
                return;
            }

            var window = GetWindow<ProjectToolsWindow>();
            window.titleContent = new GUIContent("Project Tools");
            window.minSize = new Vector2(420, 500);
        }

        public void CreateGUI()
        {
            _options = LoadOptions();
            BuildUI();
        }

        public void SetStatus(string message, bool isError)
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text = message;

            _statusLabel.style.color = isError
                ? new Color(1f, 0.3f, 0.3f)
                : new Color(0.7f, 0.7f, 0.7f);
        }

        private void OnEnable()
        {
            _tabs = CreateTabs();

            foreach (var tab in _tabs)
                tab.OnEnable();
        }

        private void OnDisable()
        {
            foreach (var tab in _tabs)
                tab.OnDisable();
        }

        private void BuildUI()
        {
            _tabButtons.Clear();
            _tabContents.Clear();

            _root = new VisualElement();
            _root.AddToClassList("project-tools-root");
            rootVisualElement.Add(_root);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);

            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);

            BuildHeader();
            BuildTabBar();
            BuildTabContents();
            BuildFooter();

            SelectTab(RestoreTabIndex());
        }

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("header");

            var title = new Label("Project Tools");
            title.AddToClassList("header-title");
            header.Add(title);

            var saveButton = new Button(OnSaveClicked) { text = "Save" };
            saveButton.AddToClassList("save-button");
            header.Add(saveButton);

            _root.Add(header);
        }

        private void BuildTabBar()
        {
            var tabBar = new VisualElement();
            tabBar.AddToClassList("tab-bar");

            for (int i = 0; i < _tabs.Count; i++)
            {
                int index = i;
                var button = new Button(() => SelectTab(index)) { text = _tabs[i].Title };
                button.AddToClassList("tab-button");
                tabBar.Add(button);
                _tabButtons.Add(button);
            }

            _root.Add(tabBar);
        }

        private void BuildTabContents()
        {
            _scrollHost = new ScrollView(ScrollViewMode.Vertical);
            _scrollHost.AddToClassList("tab-content-wrapper");
            _root.Add(_scrollHost);

            _fillHost = new VisualElement();
            _fillHost.AddToClassList("tab-content-wrapper");
            _root.Add(_fillHost);

            for (var i = 0; i < _tabs.Count; i++)
                _tabContents.Add(null);
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("footer");

            _statusLabel = new Label("Ready");
            _statusLabel.AddToClassList("status-label");
            footer.Add(_statusLabel);

            _root.Add(footer);
        }

        private void SelectTab(int index)
        {
            var selected = _tabs[index];

            if (_tabContents[index] == null)
            {
                var content = selected.Build(this);
                (selected.FillsWindow ? _fillHost : _scrollHost).Add(content);
                _tabContents[index] = content;
            }

            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].EnableInClassList("tab-button--active", i == index);

            for (int i = 0; i < _tabContents.Count; i++)
            {
                if (_tabContents[i] != null)
                    _tabContents[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
            }

            _scrollHost.style.display = selected.FillsWindow ? DisplayStyle.None : DisplayStyle.Flex;
            _fillHost.style.display = selected.FillsWindow ? DisplayStyle.Flex : DisplayStyle.None;

            EditorPrefs.SetString(ActiveTabPrefsKey, selected.Title);
        }

        private int RestoreTabIndex()
        {
            var title = EditorPrefs.GetString(ActiveTabPrefsKey, string.Empty);
            var index = _tabs.FindIndex(tab => tab.Title == title);
            return index >= 0 ? index : 0;
        }

        private static List<ProjectToolsTab> CreateTabs()
        {
            var declared = new List<(int order, ProjectToolsTab tab)>();

            foreach (var type in TypeCache.GetTypesWithAttribute<ProjectToolsTabAttribute>())
            {
                if (type.IsAbstract || typeof(ProjectToolsTab).IsAssignableFrom(type) == false)
                    throw new InvalidOperationException(
                        $"[ProjectTools] {type.FullName}: [ProjectToolsTab] marks a non-abstract {nameof(ProjectToolsTab)}");

                var order = type.GetCustomAttribute<ProjectToolsTabAttribute>().Order;
                declared.Add((order, (ProjectToolsTab)Activator.CreateInstance(type)));
            }

            declared.Sort((a, b) => a.order != b.order
                ? a.order.CompareTo(b.order)
                : string.CompareOrdinal(a.tab.Title, b.tab.Title));

            var tabs = new List<ProjectToolsTab>(declared.Count);

            foreach (var (_, tab) in declared)
                tabs.Add(tab);

            return tabs;
        }

        private void OnSaveClicked()
        {
            if (_options == null)
            {
                SetStatus("Options asset is missing", true);
                return;
            }

            EditorUtility.SetDirty(_options);
            AssetDatabase.SaveAssets();
            SetStatus("Settings saved", false);
            _log.Info("Settings saved");
        }

        private OptionsContainer LoadOptions()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(OptionsContainer)}");

            if (guids.Length == 0)
            {
                _log.Error("OptionsContainer asset is missing");
                return null;
            }

            if (guids.Length > 1)
                _log.Warning("Found {Count} OptionsContainer assets, using the first one", guids.Length);

            return AssetDatabase.LoadAssetAtPath<OptionsContainer>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}