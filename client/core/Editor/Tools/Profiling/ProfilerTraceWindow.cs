using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Internal
{
    /// <summary>
    /// Loading trace waterfall: the scope list as a tree on the left and their spans on a shared
    /// timeline on the right. Traces are written by <see cref="ProfilerTraceStorage"/> in Play Mode,
    /// the window only reads them.
    /// </summary>
    public class ProfilerTraceWindow : EditorWindow
    {
        private List<string> _files = new();
        private string _external;
        private ProfilerTraceData _trace;
        private string _selectedFile;

        private ToolbarMenu _tracesMenu;
        private ToolbarButton _copyPath;
        private ProfilerTraceAxis _axis;
        private ProfilerTraceRows _rows;

        [MenuItem(FrameworkMenu.Root + "Startup Profiler", priority = FrameworkMenu.GroupPriority)]
        public static void Open()
        {
            var window = GetWindow<ProfilerTraceWindow>();
            window.titleContent = new GUIContent("Startup Profiler");
            window.minSize = new Vector2(720f, 300f);
            window.Show();
        }

        private void OnEnable()
        {
            ProfilerTraceStorage.Saved += OnTraceSaved;
        }

        private void OnDisable()
        {
            ProfilerTraceStorage.Saved -= OnTraceSaved;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = new Color(0.16f, 0.17f, 0.19f);

            var summary = new Label
            {
                style =
                {
                    color = new Color(0.72f, 0.74f, 0.78f),
                    paddingLeft = 8f,
                    paddingTop = 6f,
                    paddingBottom = 6f,
                    fontSize = 12f
                }
            };

            _axis = new ProfilerTraceAxis();
            _rows = new ProfilerTraceRows(summary);

            root.Add(CreateToolbar());
            root.Add(summary);
            root.Add(_axis.Element);
            root.Add(_rows.Element);

            Refresh();
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();

            var refresh = new ToolbarButton(Refresh) { text = "Refresh" };
            toolbar.Add(refresh);

            _tracesMenu = new ToolbarMenu { text = "Traces" };
            toolbar.Add(_tracesMenu);

            // Frame capture keeps profiler recording on and its buffer takes gigabytes,
            // so it is off until requested here, for this editor session.
            var frames = new ToolbarToggle { text = "Frames", value = ProfilerFrameCapture.Enabled };
            frames.tooltip = "Снимать сэмплы Unity-профайлера по кадрам трассы. Включает запись профайлера до закрытия редактора";
            frames.RegisterValueChangedCallback(evt => ProfilerFrameCapture.SetEnabled(evt.newValue));
            toolbar.Add(frames);

            toolbar.Add(new ToolbarButton(_rows.ExpandAll) { text = "Expand all" });
            toolbar.Add(new ToolbarButton(_rows.CollapseAll) { text = "Collapse all" });

            toolbar.Add(new ToolbarButton(() => {
                if (EditorUtility.DisplayDialog("Startup Profiler", "Удалить все сохранённые трассы?", "Удалить",
                        "Отмена") ==
                    false)
                    return;

                ProfilerTraceStorage.Clear();
                _trace = null;
                _selectedFile = null;
                Refresh();
            }) { text = "Clear" });

            toolbar.Add(new ToolbarButton(() => EditorUtility.RevealInFinder(ProfilerTraceStorage.Directory))
                { text = "Open folder" });

            // The path to the open trace is needed to take it into a chat or another tool:
            // there are two directories, and the file name is a timestamp, nobody types that by hand.
            _copyPath = new ToolbarButton(CopySelectedPath) { text = "Copy path" };
            toolbar.Add(_copyPath);

            // A trace may come from another machine or a device - then it is simply
            // opened as a file, bypassing both known directories.
            toolbar.Add(new ToolbarButton(() => {
                var path = EditorUtility.OpenFilePanel("Open trace", ProfilerTraceStorage.Directory, "json");

                if (string.IsNullOrEmpty(path))
                    return;

                _external = path;
                _selectedFile = path;

                Refresh();
            }) { text = "Open file…" });

            return toolbar;
        }

        private void OnTraceSaved(ProfilerTraceData trace)
        {
            _selectedFile = null;
            EditorApplication.delayCall += Refresh;
        }

        private void Refresh()
        {
            if (_rows == null)
                return;

            _files = ProfilerTraceStorage.List()
                                         .Where(file => string.IsNullOrEmpty(file) == false)
                                         .ToList();

            if (string.IsNullOrEmpty(_external) == false && _files.Contains(_external) == false)
                _files.Insert(0, _external);

            if (string.IsNullOrEmpty(_selectedFile) || _files.Contains(_selectedFile) == false)
                _selectedFile = _files.FirstOrDefault();

            _trace = _selectedFile == null ? ProfilerTraceStorage.Last : ProfilerTraceStorage.Load(_selectedFile);

            RebuildTracesMenu();
            _axis.Rebuild(_trace);
            _rows.Show(_trace);

            // A trace from ProfilerTraceStorage.Last lives in memory and has no file behind it.
            _copyPath?.SetEnabled(string.IsNullOrEmpty(_selectedFile) == false);
        }

        private void CopySelectedPath()
        {
            if (string.IsNullOrEmpty(_selectedFile))
                return;

            var path = Path.GetFullPath(_selectedFile);

            EditorGUIUtility.systemCopyBuffer = path;
            ShowNotification(new GUIContent("Path copied"));
            Debug.Log($"[Profiler] Trace path copied: {path}");
        }

        private void RebuildTracesMenu()
        {
            _tracesMenu.menu.ClearItems();

            if (_files.Count == 0)
            {
                _tracesMenu.text = "No traces";
                return;
            }

            _tracesMenu.text = Describe(_selectedFile);

            foreach (var file in _files)
            {
                var captured = file;

                _tracesMenu.menu.AppendAction(
                    Describe(file),
                    _ => {
                        _selectedFile = captured;
                        Refresh();
                    },
                    _ => captured == _selectedFile
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
        }

        /// <summary>
        /// Traces from a build live in persistentDataPath and are mixed in the list with
        /// editor ones, so the source is written right in the menu item.
        /// </summary>
        private static string Describe(string file)
        {
            // An empty path crashes Path.GetDirectoryName, and it can come both from the list
            // and from a cancelled open file dialog.
            if (string.IsNullOrEmpty(file))
                return "trace";

            var name = Path.GetFileNameWithoutExtension(file);
            var directory = Path.GetDirectoryName(file);

            if (string.IsNullOrEmpty(directory))
                return name;

            if (directory == ProfilerTraceStorage.Directory)
                return $"editor · {name}";

            return Path.GetFullPath(directory).Contains("ProfilerTraces")
                ? $"build · {name}"
                : $"file · {name}";
        }
    }
}
