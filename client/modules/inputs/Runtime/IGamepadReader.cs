using UnityEngine;

namespace Global.Inputs
{
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
}
