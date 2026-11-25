using UnityEngine;

namespace Global.Inputs
{
    // A step of the selection in a UI from the gamepad: the d-pad, or the left stick deflected further than
    // InputOptions.NavigationDeflection, snapped to its dominant axis (Y up). Zero - no direction. The d-pad wins over
    // the stick.
    public static class GamepadNavigation
    {
        public static Vector2Int GetDirection(IGamepadReader gamepad, InputOptions options)
        {
            var dpad = new Vector2Int(
                Axis(gamepad.IsPressed(GamepadButton.DpadRight), gamepad.IsPressed(GamepadButton.DpadLeft)),
                Axis(gamepad.IsPressed(GamepadButton.DpadUp), gamepad.IsPressed(GamepadButton.DpadDown)));

            if (dpad != Vector2Int.zero)
                return Snap(dpad);

            var stick = gamepad.LeftStick;

            if (stick.magnitude <= options.NavigationDeflection)
                return Vector2Int.zero;

            return Mathf.Abs(stick.x) >= Mathf.Abs(stick.y)
                ? new Vector2Int(stick.x > 0f ? 1 : -1, 0)
                : new Vector2Int(0, stick.y > 0f ? 1 : -1);
        }

        private static int Axis(bool positive, bool negative)
        {
            return (positive == true ? 1 : 0) - (negative == true ? 1 : 0);
        }

        // Two directions of the d-pad at once (a diagonal): the vertical one, the lists are vertical.
        private static Vector2Int Snap(Vector2Int direction)
        {
            return direction.y != 0 ? new Vector2Int(0, direction.y) : direction;
        }
    }
}
