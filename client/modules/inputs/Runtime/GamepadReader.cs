using System;
using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Global.Inputs
{
    // Buttons of a gamepad by meaning, named as on Xbox (the W3C standard gamepad layout). The triggers and the
    // directions of the d-pad are buttons too: a trigger is pressed beyond InputOptions.TriggerPressThreshold.
    public enum GamepadButton
    {
        A,
        B,
        X,
        Y,
        LeftBumper,
        RightBumper,
        LeftTrigger,
        RightTrigger,
        View,
        Menu,
        LeftStick,
        RightStick,
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight
    }

    // One sample of the gamepad: sticks -1..1 with Y up, triggers 0..1, raw (no dead zones). Buttons hold
    // a bit per GamepadButton. A gamepad that is not connected reads as all zero.
    public struct GamepadState
    {
        public Vector2 LeftStick { get; private set; }
        public Vector2 RightStick { get; private set; }
        public float LeftTrigger { get; private set; }
        public float RightTrigger { get; private set; }
        public int Buttons { get; private set; }

        public void SetLeftStick(Vector2 value)
        {
            LeftStick = value;
        }

        public void SetRightStick(Vector2 value)
        {
            RightStick = value;
        }

        public void SetLeftTrigger(float value)
        {
            LeftTrigger = value;
        }

        public void SetRightTrigger(float value)
        {
            RightTrigger = value;
        }

        public bool IsPressed(GamepadButton button)
        {
            return (Buttons & (1 << (int)button)) != 0;
        }

        public void SetPressed(GamepadButton button, bool isPressed)
        {
            if (isPressed == true)
                Buttons |= 1 << (int)button;
            else
                Buttons &= ~(1 << (int)button);
        }
    }

    // The gamepad through the old Input Manager, by meaning, not by axis names and button numbers: those differ
    // between WebGL and the Linux editor.
    // All joysticks are read as one. The state is sampled once a frame, on the first read of the frame, so
    // WasPressed / WasReleased are edges between two sampled frames and do not depend on the update order.
    public interface IGamepadReader
    {
        GamepadState State { get; }

        Vector2 LeftStick { get; }
        Vector2 RightStick { get; }
        float LeftTrigger { get; }
        float RightTrigger { get; }

        bool IsPressed(GamepadButton button);

        // Pressed in this frame, not in the previous sample.
        bool WasPressed(GamepadButton button);

        // Released in this frame, pressed in the previous sample.
        bool WasReleased(GamepadButton button);

        // A button of the gamepad was pressed in this frame.
        bool WasAnyPressed { get; }
    }

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
            return "Gamepad Axis " + axis;
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

            _substitute = sample;

            lifetime.Listen(() => _substitute = null);
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

    // Where the old Input Manager puts the W3C standard gamepad on a platform. Axes are 1-based ("Nth axis"),
    // buttons are KeyCode.JoystickButtonN of any joystick. Taken from the players of Unity 7000.0.0a10:
    // - WebGL: InputProcess() in the WebGL player (PlatformDependent/WebGL/Source/Input), standard mapping only;
    // - Linux editor and player: SDL GameController events (processControllerAxisMotionEvent,
    //   processControllerButtonEvent, processDpadEvent in the editor binary);
    // - Windows player (Steam): XInput, the numbers of the Unity XInput mapping coincide with the WebGL ones (axes 1/2,
    //   4/5, d-pad 6/7 with up +, triggers 9/10; View 6, Menu 7, sticks 8/9).
    public class GamepadLayout
    {
        private GamepadLayout(KeyCode[] keys)
        {
            _keys = keys;
        }

        private readonly KeyCode[] _keys;

        public int LeftX { get; private set; }
        public int LeftY { get; private set; }
        public int RightX { get; private set; }
        public int RightY { get; private set; }
        public int LeftTrigger { get; private set; }
        public int RightTrigger { get; private set; }
        public int DpadX { get; private set; }
        public int DpadY { get; private set; }

        // The sign of up on the d-pad Y axis: +1 in WebGL, -1 on Linux.
        public float DpadYUp { get; private set; }

        // A platform without a layout has no gamepad here: AddInput registers NoGamepadReader there.
        public static bool HasLayout(RuntimePlatform platform)
        {
            return platform switch
            {
                RuntimePlatform.WebGLPlayer => true,
                RuntimePlatform.LinuxEditor => true,
                RuntimePlatform.LinuxPlayer => true,
                RuntimePlatform.WindowsEditor => true,
                RuntimePlatform.WindowsPlayer => true,
                _ => false
            };
        }

        public static GamepadLayout ForPlatform(RuntimePlatform platform)
        {
            return platform switch
            {
                RuntimePlatform.WebGLPlayer => Web(),
                RuntimePlatform.LinuxEditor => Sdl(),
                RuntimePlatform.LinuxPlayer => Sdl(),
                RuntimePlatform.WindowsPlayer => Web(),
                RuntimePlatform.WindowsEditor => Web(),
                _ => throw new NotSupportedException($"No gamepad layout of the old Input Manager for {platform}")
            };
        }

        public KeyCode GetKey(GamepadButton button)
        {
            var key = _keys[(int)button];

            if (key == KeyCode.None)
                throw new ArgumentOutOfRangeException(nameof(button), button, "The button is not a joystick button");

            return key;
        }

        // Axes: 1 left X, 2 left Y (down +), 3 LT - RT, 4 right X, 5 right Y (down +), 6 d-pad X (right +),
        // 7 d-pad Y (up +), 9 LT, 10 RT. Buttons: the W3C index, but View 8 -> 6, Menu 9 -> 7, left stick 10 -> 8,
        // right stick 11 -> 9; the triggers (6, 7) are not buttons, the d-pad is also buttons 12-15.
        private static GamepadLayout Web()
        {
            return new GamepadLayout(Keys(view: 6, menu: 7, leftStick: 8, rightStick: 9))
            {
                LeftX = 1,
                LeftY = 2,
                RightX = 4,
                RightY = 5,
                LeftTrigger = 9,
                RightTrigger = 10,
                DpadX = 6,
                DpadY = 7,
                DpadYUp = 1f
            };
        }

        // Axes: 1 left X, 2 left Y (down +), 3 LT, 4 right X, 5 right Y (down +), 6 RT, 7 d-pad X (right +),
        // 8 d-pad Y (down +). Buttons: A 0, B 1, X 2, Y 3, LB 4, RB 5, View 6, Menu 7, Guide 8, left stick 9,
        // right stick 10.
        private static GamepadLayout Sdl()
        {
            return new GamepadLayout(Keys(view: 6, menu: 7, leftStick: 9, rightStick: 10))
            {
                LeftX = 1,
                LeftY = 2,
                RightX = 4,
                RightY = 5,
                LeftTrigger = 3,
                RightTrigger = 6,
                DpadX = 7,
                DpadY = 8,
                DpadYUp = -1f
            };
        }

        private static KeyCode[] Keys(int view, int menu, int leftStick, int rightStick)
        {
            var keys = new KeyCode[Enum.GetValues(typeof(GamepadButton)).Length];

            keys[(int)GamepadButton.A] = Button(0);
            keys[(int)GamepadButton.B] = Button(1);
            keys[(int)GamepadButton.X] = Button(2);
            keys[(int)GamepadButton.Y] = Button(3);
            keys[(int)GamepadButton.LeftBumper] = Button(4);
            keys[(int)GamepadButton.RightBumper] = Button(5);
            keys[(int)GamepadButton.View] = Button(view);
            keys[(int)GamepadButton.Menu] = Button(menu);
            keys[(int)GamepadButton.LeftStick] = Button(leftStick);
            keys[(int)GamepadButton.RightStick] = Button(rightStick);

            return keys;
        }

        private static KeyCode Button(int index)
        {
            return KeyCode.JoystickButton0 + index;
        }
    }
}
