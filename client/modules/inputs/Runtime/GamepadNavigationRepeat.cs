using UnityEngine;

namespace Global.Inputs
{
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
