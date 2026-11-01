using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Global.UI
{
    // What the gamepad can select in a window, collected anew each frame: an active, interactable selectable under the
    // root with a navigation mode other than None, not an input field (typing is not done with a gamepad).
    internal sealed class NavigationCandidates
    {
        private readonly List<Selectable> _found = new();
        private readonly List<Selectable> _candidates = new();

        public List<Selectable>.Enumerator GetEnumerator()
        {
            return _candidates.GetEnumerator();
        }

        public void Collect(RectTransform root)
        {
            root.GetComponentsInChildren(false, _found);
            _candidates.Clear();

            foreach (var selectable in _found)
            {
                if (IsCandidate(selectable) == true)
                    _candidates.Add(selectable);
            }
        }

        public bool Contains(Selectable selectable)
        {
            return _candidates.Contains(selectable);
        }

        public Selectable Find(GameObject selected)
        {
            foreach (var candidate in _candidates)
            {
                if (candidate.gameObject == selected)
                    return candidate;
            }

            return null;
        }

        public Selectable First(RectTransform start)
        {
            foreach (var candidate in _candidates)
            {
                if (candidate.transform.IsChildOf(start) == true)
                    return candidate;
            }

            return _candidates.Count > 0 ? _candidates[0] : null;
        }

        private static bool IsCandidate(Selectable selectable)
        {
            return selectable.isActiveAndEnabled == true
                   && selectable.IsInteractable() == true
                   && selectable.navigation.mode != Navigation.Mode.None
                   && selectable is not TMP_InputField;
        }
    }
}
