using System;
using System.Collections.Generic;
using Global.Inputs;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Global.UI
{
    // In the Gamepad scheme the selection is kept in the window on top: the one selected in it before (when it is
    // still there) or its first candidate in the order of the hierarchy (NavigationCandidates). The steps are uGUI ones
    // (StandaloneInputModule, GamepadUIInput) over the navigation set on the selected one (SelectionNavigationBinding).
    // A selection in a scroll view is scrolled into it (ScrollIntoSelection).
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

        private readonly IUpdater _updater;
        private readonly IInputScheme _scheme;
        private readonly IGamepadReader _gamepad;
        private readonly InputOptions _options;

        private readonly List<NavigationWindow> _windows = new();
        private readonly List<NavigationWindow> _overlays = new();
        private readonly NavigationCandidates _candidates = new();
        private readonly SelectionNavigationBinding _binding = new();

        private EventSystem _eventSystem;
        private GamepadUIInput _input;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // The event system of the global scope (AddInput instantiates Global_Events) is enabled by now.
            _eventSystem = EventSystem.current;

            if (_eventSystem == null)
                throw new MissingReferenceException("[UINavigation] No event system");

            if (_eventSystem.TryGetComponent(out StandaloneInputModule module) == false)
                throw new MissingComponentException("[UINavigation] The event system has no StandaloneInputModule");

            // The override the event system had is given back, and the component removed, when the scope ends: a rebuilt
            // scope does not leave the module on a dead reader.
            var previous = module.inputOverride;
            var input = _eventSystem.gameObject.AddComponent<GamepadUIInput>();
            input.Construct(_scheme, _gamepad, _options, module);
            _input = input;

            lifetime.Listen(() =>
            {
                if (module != null && module.inputOverride == input)
                    module.inputOverride = previous;

                if (input != null)
                    UnityEngine.Object.Destroy(input);
            });

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

            var window = new NavigationWindow(root, start);

            _windows.Add(window);
            lifetime.Listen(() => _windows.Remove(window));
        }

        public void AddOverlay(IReadOnlyLifetime lifetime, RectTransform root)
        {
            var window = new NavigationWindow(root, root);

            _overlays.Add(window);
            lifetime.Listen(() => _overlays.Remove(window));
        }

        public void OnUpdate(float delta)
        {
            var selected = _eventSystem.currentSelectedGameObject;

            if (_scheme.Current.Value != InputSchemeType.Gamepad)
            {
                _binding.Release();

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

            _candidates.Collect(window.Root);

            var current = selected != null ? _candidates.Find(selected) : null;

            if (current == null)
            {
                current = window.FindRecent(_candidates) ?? _candidates.First(window.Start);

                if (current == null)
                {
                    Deselect(selected);
                    return;
                }

                _binding.Bind(current, _candidates);
                _eventSystem.SetSelectedGameObject(current.gameObject);
                _input.SuppressPress();
            }
            else
            {
                _binding.Bind(current, _candidates);
            }

            window.Remember(current);
            ScrollIntoSelection.Apply(current);
        }

        private NavigationWindow GetTop()
        {
            return GetTop(_overlays) ?? GetTop(_windows);
        }

        private static NavigationWindow GetTop(List<NavigationWindow> windows)
        {
            for (var i = windows.Count - 1; i >= 0; i--)
            {
                var root = windows[i].Root;

                // the scene of a screen can be unloaded before the lifetime of its window ends
                if (root != null && root.gameObject.activeInHierarchy == true)
                    return windows[i];
            }

            return null;
        }

        private void Deselect(GameObject selected)
        {
            _binding.Release();

            if (selected != null)
                _eventSystem.SetSelectedGameObject(null);
        }
    }
}
