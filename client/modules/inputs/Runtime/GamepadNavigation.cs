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

    // Steps of a held direction (GamepadNavigation.GetDirection): one at the press, then after
    // InputOptions.NavigationRepeatDelay one every NavigationRepeatInterval, real time. A new direction is a new press.
    // For a UI that moves its own selection (the shop window); uGUI repeats the steps itself (StandaloneInputModule).
    public class GamepadNavigationRepeat
    {
        public GamepadNavigationRepeat(InputOptions options)
        {
            _options = options;
        }

        private readonly InputOptions _options;

        private Vector2Int _held;
        private float _nextTime;

        public Vector2Int Step(Vector2Int direction)
        {
            var now = Time.unscaledTime;
            var options = _options;

            if (direction == Vector2Int.zero)
            {
                _held = Vector2Int.zero;
                return Vector2Int.zero;
            }

            if (direction != _held)
            {
                _held = direction;
                _nextTime = now + options.NavigationRepeatDelay;
                return direction;
            }

            if (now < _nextTime)
                return Vector2Int.zero;

            _nextTime = now + options.NavigationRepeatInterval;
            return direction;
        }

        // The held direction is taken as handled: it steps again only after a release (a window that has just opened
        // does not move by the stick that was held before it).
        public void Hold(Vector2Int direction)
        {
            _held = direction;
            _nextTime = float.PositiveInfinity;
        }
    }
}
