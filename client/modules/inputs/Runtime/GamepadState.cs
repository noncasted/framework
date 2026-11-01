using UnityEngine;

namespace Global.Inputs
{
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
}
