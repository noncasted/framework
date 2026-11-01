using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Global.UI
{
    // A root the selection can be in, with the memory of what was selected in it.
    internal sealed class NavigationWindow
    {
        public NavigationWindow(RectTransform root, RectTransform start)
        {
            Root = root;
            Start = start;
        }

        // The recent selections of the window, the newest last. A panel shown in place of another one under the
        // same root (Join in place of the main menu) takes the selection; when it hides, the selection goes back to
        // the newest one still there - the button that opened the panel, not the first of the window.
        private const int HistorySize = 8;

        private readonly List<Selectable> _history = new();

        public RectTransform Root { get; }
        public RectTransform Start { get; }

        public void Remember(Selectable selected)
        {
            if (_history.Count > 0 && _history[^1] == selected)
                return;

            _history.Remove(selected);
            _history.Add(selected);

            if (_history.Count > HistorySize)
                _history.RemoveAt(0);
        }

        public Selectable FindRecent(NavigationCandidates candidates)
        {
            for (var i = _history.Count - 1; i >= 0; i--)
            {
                if (candidates.Contains(_history[i]) == true)
                    return _history[i];
            }

            return null;
        }
    }
}
