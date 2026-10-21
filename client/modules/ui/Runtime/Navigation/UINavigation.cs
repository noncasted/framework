using System;
using System.Collections.Generic;
using Global.Inputs;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Global.UI
{
    // The selection of the uGUI buttons by the gamepad. A screen or a window adds its root while it is open;
    // the window on top is the last added one whose root is active in the hierarchy (a panel opened in place of
    // another one hides it, a window over a screen is added after it).
    public interface IUINavigation
    {
        void Add(IReadOnlyLifetime lifetime, RectTransform root);

        // The first selection is looked for under start first (a part of the root: the buttons of the lobby, not the
        // room code that comes before them in the hierarchy), then in the whole root.
        void Add(IReadOnlyLifetime lifetime, RectTransform root, RectTransform start);
    }

    public static class UINavigationExtensions
    {
        // The selectable can be selected by the gamepad (a candidate of the window it is in). The menu kit and its
        // prefabs have Navigation.None everywhere, which keeps a selectable out (the arrows of a slider, the field
        // around the character cards that closes the panel).
        public static void EnableNavigation(this Selectable selectable)
        {
            selectable.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        }
    }

    // In the Gamepad scheme the selection is kept in the window on top: the one selected in it before (when it is
    // still there) or its first candidate in the order of the hierarchy. A candidate is an active, interactable
    // selectable under the root with a navigation mode other than None, not an input field (typing is not done with
    // a gamepad: the fields are skipped). The steps are uGUI ones (StandaloneInputModule, GamepadUIInput), but the
    // navigation of the selected one is set here each frame, explicit, to the nearest candidate of the same window in
    // each direction (the automatic one of uGUI looks at all the selectables of all canvases); a horizontal slider
    // keeps left and right for its value. A selection in a scroll view is scrolled into it.
    // In the other schemes, and with no window, nothing is selected (an input field keeps its focus but in Gamepad):
    // a mouse click selects a button too, and a selection is shown as the hover.
    public class UINavigation : IUINavigation, IScopeSetup, IUpdatable
    {
        public UINavigation(IUpdater updater, IInputScheme scheme, IGamepadReader gamepad, InputOptions options)
        {
            _updater = updater;
            _scheme = scheme;
            _gamepad = gamepad;
            _options = options;
        }

        private static readonly Vector2[] Directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        private const float SidewaysWeight = 2f;

        private readonly IUpdater _updater;
        private readonly IInputScheme _scheme;
        private readonly IGamepadReader _gamepad;
        private readonly InputOptions _options;

        private readonly List<Window> _windows = new();
        private readonly List<Selectable> _found = new();
        private readonly List<Selectable> _candidates = new();

        private EventSystem _eventSystem;
        private GamepadUIInput _input;

        // The selectable whose navigation is set here, and its own one to give back when the selection leaves it.
        private Selectable _navigated;
        private Navigation _navigatedOwn;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // The event system of the global scope (AddInput instantiates Global_Events) is enabled by now.
            _eventSystem = EventSystem.current;

            if (_eventSystem == null)
                throw new MissingReferenceException("[UINavigation] No event system");

            if (_eventSystem.TryGetComponent(out StandaloneInputModule module) == false)
                throw new MissingComponentException("[UINavigation] The event system has no StandaloneInputModule");

            _input = _eventSystem.gameObject.AddComponent<GamepadUIInput>();
            _input.Construct(_scheme, _gamepad, _options, module);

            _updater.Add(lifetime, this);
        }

        public void Add(IReadOnlyLifetime lifetime, RectTransform root)
        {
            Add(lifetime, root, root);
        }

        public void Add(IReadOnlyLifetime lifetime, RectTransform root, RectTransform start)
        {
            if (start.IsChildOf(root) == false)
                throw new ArgumentException($"[UINavigation] {start.name} is not under {root.name}", nameof(start));

            var window = new Window(root, start);

            _windows.Add(window);
            lifetime.Listen(() => _windows.Remove(window));
        }

        public void OnUpdate(float delta)
        {
            var selected = _eventSystem.currentSelectedGameObject;

            if (_scheme.Current.Value != InputSchemeType.Gamepad)
            {
                Release();

                if (selected != null && selected.TryGetComponent(out TMP_InputField _) == false)
                    _eventSystem.SetSelectedGameObject(null);

                return;
            }

            var window = GetTop();

            if (window == null)
            {
                Deselect(selected);
                return;
            }

            Collect(window.Root);

            var current = selected != null ? FindCandidate(selected) : null;

            if (current == null)
            {
                current = window.FindRecent(_candidates) ?? First(window.Start);

                if (current == null)
                {
                    Deselect(selected);
                    return;
                }

                Navigate(current);
                _eventSystem.SetSelectedGameObject(current.gameObject);
                _input.SuppressPress();
            }
            else
            {
                Navigate(current);
            }

            window.Remember(current);
            ScrollIntoView(current);
        }

        private Window GetTop()
        {
            for (var i = _windows.Count - 1; i >= 0; i--)
            {
                var root = _windows[i].Root;

                // the scene of a screen can be unloaded before the lifetime of its window ends
                if (root != null && root.gameObject.activeInHierarchy == true)
                    return _windows[i];
            }

            return null;
        }

        private void Deselect(GameObject selected)
        {
            Release();

            if (selected != null)
                _eventSystem.SetSelectedGameObject(null);
        }

        private void Collect(RectTransform root)
        {
            root.GetComponentsInChildren(false, _found);
            _candidates.Clear();

            foreach (var selectable in _found)
            {
                if (IsCandidate(selectable) == true)
                    _candidates.Add(selectable);
            }
        }

        private static bool IsCandidate(Selectable selectable)
        {
            return selectable.isActiveAndEnabled == true
                   && selectable.IsInteractable() == true
                   && selectable.navigation.mode != Navigation.Mode.None
                   && selectable is not TMP_InputField;
        }

        private Selectable FindCandidate(GameObject selected)
        {
            foreach (var candidate in _candidates)
            {
                if (candidate.gameObject == selected)
                    return candidate;
            }

            return null;
        }

        private Selectable First(RectTransform start)
        {
            foreach (var candidate in _candidates)
            {
                if (candidate.transform.IsChildOf(start) == true)
                    return candidate;
            }

            return _candidates.Count > 0 ? _candidates[0] : null;
        }

        private void Navigate(Selectable current)
        {
            if (_navigated != current)
            {
                Release();
                _navigated = current;
                _navigatedOwn = current.navigation;
            }

            var keepsHorizontal = current is Slider slider &&
                                  (slider.direction == Slider.Direction.LeftToRight ||
                                   slider.direction == Slider.Direction.RightToLeft);

            current.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = FindNearest(current, Directions[0]),
                selectOnDown = FindNearest(current, Directions[1]),
                selectOnLeft = keepsHorizontal == true ? null : FindNearest(current, Directions[2]),
                selectOnRight = keepsHorizontal == true ? null : FindNearest(current, Directions[3])
            };
        }

        private void Release()
        {
            // a destroyed one has nothing to give back
            if (_navigated != null)
                _navigated.navigation = _navigatedOwn;

            _navigated = null;
        }

        // From the middle of the edge of the current one in the direction, to the nearest point of a candidate: the
        // candidate is ahead (its centre past the edge), and the cheapest is the one nearest along the direction with
        // the sideways distance counted twice. The nearest point, not the centre (the rule of uGUI): a wide row right
        // under a small cell is the next one down, not a row further down with its centre nearer by the side.
        // A candidate that lies over the current one (the "Back" of the character panel over the bottom of the middle
        // card) is not ahead of its edge: when nothing is, the search is repeated from the centre of the current one.
        private Selectable FindNearest(Selectable current, Vector2 direction)
        {
            var rect = (RectTransform)current.transform;
            var local = rect.rect;
            var edge = (Vector2)rect.TransformPoint(local.center + Vector2.Scale(local.size * 0.5f, direction));
            var best = FindNearest(current, direction, edge);

            return best != null ? best : FindNearest(current, direction, (Vector2)rect.TransformPoint(local.center));
        }

        private Selectable FindNearest(Selectable current, Vector2 direction, Vector2 from)
        {
            Selectable best = null;
            var bestCost = float.PositiveInfinity;

            foreach (var candidate in _candidates)
            {
                if (candidate == current)
                    continue;

                var candidateRect = (RectTransform)candidate.transform;
                var bounds = candidateRect.rect;
                var min = (Vector2)candidateRect.TransformPoint(bounds.min);
                var max = (Vector2)candidateRect.TransformPoint(bounds.max);
                var centre = (min + max) * 0.5f;

                if (Vector2.Dot(direction, centre - from) <= 0f)
                    continue;

                var nearest = new Vector2(Mathf.Clamp(from.x, min.x, max.x), Mathf.Clamp(from.y, min.y, max.y));
                var offset = nearest - from;
                var along = Vector2.Dot(direction, offset);

                if (along < 0f)
                    continue;

                var sideways = (offset - direction * along).magnitude;
                var cost = along + sideways * SidewaysWeight;

                if (cost >= bestCost)
                    continue;

                best = candidate;
                bestCost = cost;
            }

            return best;
        }

        // A vertical scroll view (the list of open lobbies) moves its content so that the selected row is seen.
        private static void ScrollIntoView(Selectable current)
        {
            var scroll = current.GetComponentInParent<ScrollRect>();

            if (scroll == null || scroll.vertical == false)
                return;

            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, current.transform);
            var view = viewport.rect;
            var shift = 0f;

            if (bounds.max.y > view.yMax)
                shift = view.yMax - bounds.max.y;
            else if (bounds.min.y < view.yMin)
                shift = view.yMin - bounds.min.y;

            if (shift == 0f)
                return;

            var content = scroll.content;
            content.anchoredPosition += new Vector2(0f, shift);
            scroll.velocity = Vector2.zero;
        }

        private class Window
        {
            public Window(RectTransform root, RectTransform start)
            {
                Root = root;
                Start = start;
            }

            public RectTransform Root { get; }
            public RectTransform Start { get; }

            // The recent selections of the window, the newest last. A panel shown in place of another one under the
            // same root (Join in place of the main menu) takes the selection; when it hides, the selection goes back to
            // the newest one still there - the button that opened the panel, not the first of the window.
            private const int HistorySize = 8;

            private readonly List<Selectable> _history = new();

            public void Remember(Selectable selected)
            {
                if (_history.Count > 0 && _history[^1] == selected)
                    return;

                _history.Remove(selected);
                _history.Add(selected);

                if (_history.Count > HistorySize)
                    _history.RemoveAt(0);
            }

            public Selectable FindRecent(List<Selectable> candidates)
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
}
