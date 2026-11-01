using System;
using UnityEngine;

namespace Global.Inputs
{
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
