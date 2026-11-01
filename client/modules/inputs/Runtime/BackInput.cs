using System;
using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Global.Inputs
{
    public enum BackInputKind
    {
        // Close the window on top, go back a step: Esc, gamepad B, gamepad Menu (Start).
        Back,

        // Open the pause (and, as Back, close what is open): Esc, gamepad Menu (Start). Gamepad B is not a pause.
        Pause
    }

    // The one "back / pause" signal of the game, for every scheme and outside a match too (menus, panels, windows).
    // A frame signal, polled like Input.GetKeyDown: true during the whole frame of the press for every reader.
    // A pause is a back too: IsBackPressed is true whenever IsPausePressed is.
    // A press of a button on the screen (touch pause, Telegram BackButton) goes through Press: it is seen by all
    // readers in the next frame, so the order of the click and the readers within a frame does not matter.
    public interface IBackInput
    {
        bool IsBackPressed { get; }
        bool IsPausePressed { get; }

        // The same signal as an event: once a frame of a press, at its start (the pre update), Pause when IsPausePressed
        // is true in this frame, otherwise Back.
        IEventSource<BackInputKind> Pressed { get; }

        // Something a back closes is open (a window, the pause, a panel): it holds a target while it is open.
        // The Telegram BackButton is shown while there is any. ListenBack adds one by itself.
        IViewableProperty<bool> HasTargets { get; }

        void Press(BackInputKind kind);
        void AddTarget(IReadOnlyLifetime lifetime);

        // Calls the action on a back while the lifetime lives, for a window that closes on a back and has no update of its
        // own. Only the listener added last is called: one press closes one window. It is a back target for the same lifetime.
        void ListenBack(IReadOnlyLifetime lifetime, Action onBack);
    }

    public class BackInput : IBackInput, IScopeSetup, IPreUpdatable
    {
        public BackInput(IUpdater updater, IGamepadReader gamepad)
        {
            _updater = updater;
            _gamepad = gamepad;
        }

        private readonly IUpdater _updater;
        private readonly IGamepadReader _gamepad;
        private readonly ViewableProperty<bool> _hasTargets = new(false);
        private readonly EventSource<BackInputKind> _pressed = new();

        // Pressed through Press: waits for the next pre update, then lives for the frame.
        private bool _pendingBack;
        private bool _pendingPause;
        private bool _pressedBack;
        private bool _pressedPause;
        private readonly List<Action> _listeners = new();
        private int _targets;

        public bool IsPausePressed =>
            _pressedPause == true || Input.GetKeyDown(KeyCode.Escape) == true || _gamepad.WasPressed(GamepadButton.Menu) == true;

        public bool IsBackPressed =>
            IsPausePressed == true || _pressedBack == true || _gamepad.WasPressed(GamepadButton.B) == true;

        public IViewableProperty<bool> HasTargets => _hasTargets;
        public IEventSource<BackInputKind> Pressed => _pressed;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void Press(BackInputKind kind)
        {
            if (kind == BackInputKind.Pause)
                _pendingPause = true;
            else
                _pendingBack = true;
        }

        public void AddTarget(IReadOnlyLifetime lifetime)
        {
            _targets++;
            _hasTargets.Set(true);

            lifetime.Listen(() =>
            {
                _targets--;
                _hasTargets.Set(_targets > 0);
            });
        }

        public void ListenBack(IReadOnlyLifetime lifetime, Action onBack)
        {
            AddTarget(lifetime);
            _listeners.Add(onBack);
            lifetime.Listen(() => _listeners.Remove(onBack));
        }

        public void OnPreUpdate(float delta)
        {
            _pressedBack = _pendingBack;
            _pressedPause = _pendingPause;
            _pendingBack = false;
            _pendingPause = false;

            if (IsPausePressed == true)
                _pressed.Invoke(BackInputKind.Pause);
            else if (IsBackPressed == true)
                _pressed.Invoke(BackInputKind.Back);
            else
                return;

            if (_listeners.Count > 0)
                _listeners[_listeners.Count - 1]();
        }
    }
}
