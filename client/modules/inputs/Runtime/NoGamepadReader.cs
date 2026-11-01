using UnityEngine;

namespace Global.Inputs
{
    // A platform without a gamepad layout of the old Input Manager (GamepadLayout.HasLayout): nothing is ever pressed.
    public class NoGamepadReader : IGamepadReader
    {
        public GamepadState State => default;
        public Vector2 LeftStick => Vector2.zero;
        public Vector2 RightStick => Vector2.zero;
        public float LeftTrigger => 0f;
        public float RightTrigger => 0f;
        public bool WasAnyPressed => false;

        public bool IsPressed(GamepadButton button)
        {
            return false;
        }

        public bool WasPressed(GamepadButton button)
        {
            return false;
        }

        public bool WasReleased(GamepadButton button)
        {
            return false;
        }
    }
}
