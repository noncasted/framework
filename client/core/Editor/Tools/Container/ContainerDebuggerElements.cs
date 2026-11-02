using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Internal
{
    // The elements repeated by the sections of the debugger: section frames, tables, hints.
    internal static class ContainerDebuggerElements
    {
        public static VisualElement Section(string sectionClass, string title, string tooltip = null)
        {
            var section = new VisualElement();
            section.AddToClassList("section");
            section.AddToClassList(sectionClass);

            var label = new Label(title);
            label.AddToClassList("section-title");
            label.tooltip = tooltip;
            section.Add(label);

            return section;
        }

        public static VisualElement ScrollBody(VisualElement section)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("section-body");

            var body = new VisualElement();
            scroll.Add(body);
            section.Add(scroll);

            return body;
        }

        public static Label PaneTitle(string text, string tooltip = null)
        {
            var title = new Label(text);
            title.AddToClassList("pane-title");
            title.tooltip = tooltip;
            return title;
        }

        public static Label Caption(string text)
        {
            var label = new Label(text);
            label.AddToClassList("graph-caption");
            return label;
        }

        public static Label Hint(string text)
        {
            var label = new Label(text);
            label.AddToClassList("hint");
            return label;
        }

        public static Column Column<T>(
            string name,
            string title,
            float width,
            float minWidth,
            bool stretchable,
            IReadOnlyList<T> items,
            Func<T, string> read,
            Func<T, string> tooltip)
        {
            return new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = minWidth,
                stretchable = stretchable,
                resizable = true,
                sortable = false,
                makeCell = MakeCell,
                bindCell = (element, index) => BindCell(element, index, items, read, tooltip)
            };
        }

        public static MultiColumnListView Table<T>(Columns columns, List<T> items, SelectionType selectionType)
        {
            var table = new MultiColumnListView(columns)
            {
                itemsSource = items,
                selectionType = selectionType,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                showBorder = true,
                fixedItemHeight = 22
            };
            table.AddToClassList("table");

            return table;
        }

        public static int FirstIndex(IEnumerable<int> indices)
        {
            foreach (var index in indices)
                return index;

            return -1;
        }

        private static Label MakeCell()
        {
            var label = new Label();
            label.AddToClassList("table-cell");
            return label;
        }

        private static void BindCell<T>(
            VisualElement element,
            int index,
            IReadOnlyList<T> items,
            Func<T, string> read,
            Func<T, string> tooltip)
        {
            var label = (Label)element;

            if ((uint)index >= (uint)items.Count)
            {
                label.text = string.Empty;
                label.tooltip = string.Empty;
                return;
            }

            var item = items[index];
            label.text = read(item);
            label.tooltip = tooltip(item);
        }
    }
}
