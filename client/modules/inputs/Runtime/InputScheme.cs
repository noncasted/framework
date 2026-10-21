using Internal;
using UnityEngine;

namespace Global.Inputs
{
    public enum InputSchemeType
    {
        KeyboardMouse,
        Gamepad,
        Touch
    }

    // The one active input scheme: the device used last. The aim (system cursor or a crosshair in the world), the
    // touch controls and the prompts follow it, and IPlayerInput reads its source of this scheme only.
    public interface IInputScheme
    {
        IViewableProperty<InputSchemeType> Current { get; }
    }

    // Switches on a meaningful input of a device, before everything else of the frame (pre update):
    // - Touch: a finger on the screen;
    // - Gamepad: a button (triggers and d-pad too) or a stick deflected beyond InputOptions.SchemeStickDeflection;
    // - KeyboardMouse: a key, a mouse button, the wheel, or the mouse moved beyond InputOptions.SchemeMouseDistance
    //   from where it stood when the scheme left KeyboardMouse.
    // The noise of a stick and small jitters of the mouse do not switch. A touch also moves the emulated mouse and
    // presses its button: in a frame with a touch the mouse is not looked at, and the mouse anchor follows it.
    // The scheme starts as Touch on a mobile device (PlatformOptions.IsMobile) or a device with a touch screen, KeyboardMouse otherwise.
    public class InputScheme : IInputScheme, IScopeSetup, IPreUpdatable
    {
        public InputScheme(IUpdater updater, IGamepadReader gamepad, InputOptions options, PlatformOptions platformOptions)
        {
            _updater = updater;
            _gamepad = gamepad;
            _options = options;

            var isTouch = platformOptions.IsMobile == true || Input.touchSupported == true;
            _current = new ViewableProperty<InputSchemeType>(isTouch ? InputSchemeType.Touch : InputSchemeType.KeyboardMouse);
        }

        private const int JoystickButtonCount = 20;

        private readonly IUpdater _updater;
        private readonly IGamepadReader _gamepad;
        private readonly InputOptions _options;
        private readonly ViewableProperty<InputSchemeType> _current;

        private Vector2 _mouseAnchor;

        public IViewableProperty<InputSchemeType> Current => _current;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _mouseAnchor = Input.mousePosition;
            _updater.Add(lifetime, this);
        }

        public void OnPreUpdate(float delta)
        {
            if (Input.touchCount > 0)
            {
                _mouseAnchor = Input.mousePosition;
                _current.Set(InputSchemeType.Touch);
                return;
            }

            if (IsGamepadUsed() == true)
            {
                _mouseAnchor = Input.mousePosition;
                _current.Set(InputSchemeType.Gamepad);
                return;
            }

            if (_current.Value == InputSchemeType.KeyboardMouse)
                return;

            if (IsKeyboardMouseUsed() == true)
                _current.Set(InputSchemeType.KeyboardMouse);
        }

        private bool IsGamepadUsed()
        {
            if (_gamepad.WasAnyPressed == true)
                return true;

            var deflection = _options.SchemeStickDeflection;

            return _gamepad.LeftStick.magnitude > deflection || _gamepad.RightStick.magnitude > deflection;
        }

        private bool IsKeyboardMouseUsed()
        {
            var mouse = (Vector2)Input.mousePosition;

            if (Vector2.Distance(mouse, _mouseAnchor) > _options.SchemeMouseDistance)
                return true;

            if (Input.mouseScrollDelta != Vector2.zero)
                return true;

            // anyKeyDown counts the mouse buttons (wanted) and the joystick buttons (not: the gamepad is read above
            // by meaning, a button it has no name for must not take the scheme to KeyboardMouse).
            if (Input.anyKeyDown == false)
                return false;

            for (var i = 0; i < JoystickButtonCount; i++)
            {
                if (Input.GetKeyDown(KeyCode.JoystickButton0 + i) == true)
                    return false;
            }

            return true;
        }
    }
}
