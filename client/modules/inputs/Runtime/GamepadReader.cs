using System;
using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Global.Inputs
{
    public class GamepadReader : IGamepadReader
    {
        public GamepadReader(InputOptions options)
        {
            RequireAxes();

            _options = options;
            _layout = GamepadLayout.ForPlatform(Application.platform);
        }

        public const int RawAxisCount = 10;

        private static readonly GamepadButton[] AllButtons = (GamepadButton[])Enum.GetValues(typeof(GamepadButton));

        // Built once: a name concatenated per read took 8 strings a frame (about 400 B).
        private static readonly string[] RawAxisNames = BuildRawAxisNames();

        private readonly InputOptions _options;
        private readonly GamepadLayout _layout;

        private Func<GamepadState> _substitute;
        private int _frame = -1;
        private GamepadState _current;
        private GamepadState _previous;

        public GamepadState State => Sample();
        public Vector2 LeftStick => Sample().LeftStick;
        public Vector2 RightStick => Sample().RightStick;
        public float LeftTrigger => Sample().LeftTrigger;
        public float RightTrigger => Sample().RightTrigger;
        public bool WasAnyPressed => (Sample().Buttons & ~_previous.Buttons) != 0;

        // The Input Manager axis of the Nth joystick axis ("Nth axis" in the Input Manager), N = 1..RawAxisCount,
        // all joysticks. ProjectSettings/InputManager.asset holds an axis of each name.
        public static string GetRawAxisName(int axis)
        {
            return RawAxisNames[axis - 1];
        }

        private static string[] BuildRawAxisNames()
        {
            var names = new string[RawAxisCount];

            for (var axis = 1; axis <= RawAxisCount; axis++)
                names[axis - 1] = "Gamepad Axis " + axis;

            return names;
        }

        // Without them every read throws, each frame, also for a player without a gamepad.
        private static void RequireAxes()
        {
            var missing = new List<string>();

            for (var axis = 1; axis <= RawAxisCount; axis++)
            {
                try
                {
                    Input.GetAxisRaw(GetRawAxisName(axis));
                }
                catch (ArgumentException)
                {
                    missing.Add(GetRawAxisName(axis));
                }
            }

            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"[Inputs] ProjectSettings/InputManager.asset has no axes: {string.Join(", ", missing)}");
        }

        public bool IsPressed(GamepadButton button)
        {
            return Sample().IsPressed(button);
        }

        public bool WasPressed(GamepadButton button)
        {
            return Sample().IsPressed(button) == true && _previous.IsPressed(button) == false;
        }

        public bool WasReleased(GamepadButton button)
        {
            return Sample().IsPressed(button) == false && _previous.IsPressed(button) == true;
        }

        // Play checks without a gamepad: while the lifetime lives the state comes from
        // sample instead of the Input Manager. The edges are still taken by the reader, a frame at a time.
        public void Substitute(IReadOnlyLifetime lifetime, Func<GamepadState> sample)
        {
            if (_substitute != null)
                throw new InvalidOperationException("The gamepad reader is substituted already");

            // The state of this frame is read again: a substitute is seen by the next read, not the next frame.
            _substitute = sample;
            _frame = -1;

            lifetime.Listen(() =>
            {
                _substitute = null;
                _frame = -1;
            });
        }

        private GamepadState Sample()
        {
            var frame = Time.frameCount;

            if (frame == _frame)
                return _current;

            _frame = frame;
            _previous = _current;
            _current = _substitute != null ? _substitute() : ReadInput();

            return _current;
        }

        private GamepadState ReadInput()
        {
            var state = new GamepadState();
            state.SetLeftStick(new Vector2(ReadAxis(_layout.LeftX), -ReadAxis(_layout.LeftY)));
            state.SetRightStick(new Vector2(ReadAxis(_layout.RightX), -ReadAxis(_layout.RightY)));
            state.SetLeftTrigger(Mathf.Clamp01(ReadAxis(_layout.LeftTrigger)));
            state.SetRightTrigger(Mathf.Clamp01(ReadAxis(_layout.RightTrigger)));

            var threshold = _options.TriggerPressThreshold;
            var dpadX = ReadAxis(_layout.DpadX);
            var dpadY = ReadAxis(_layout.DpadY) * _layout.DpadYUp;

            foreach (var button in AllButtons)
            {
                var isPressed = button switch
                {
                    GamepadButton.LeftTrigger => state.LeftTrigger > threshold,
                    GamepadButton.RightTrigger => state.RightTrigger > threshold,
                    GamepadButton.DpadUp => dpadY > 0.5f,
                    GamepadButton.DpadDown => dpadY < -0.5f,
                    GamepadButton.DpadLeft => dpadX < -0.5f,
                    GamepadButton.DpadRight => dpadX > 0.5f,
                    _ => Input.GetKey(_layout.GetKey(button))
                };

                state.SetPressed(button, isPressed);
            }

            return state;
        }

        private static float ReadAxis(int axis)
        {
            return Input.GetAxisRaw(GetRawAxisName(axis));
        }
    }
}
