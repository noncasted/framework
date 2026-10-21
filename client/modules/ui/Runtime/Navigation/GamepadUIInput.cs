using Global.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Global.UI
{
    // The input of the uGUI event system (StandaloneInputModule.inputOverride), added to the object of the event
    // system by UINavigation.
    // - The navigation of the module comes from the gamepad by meaning (IGamepadReader): the d-pad and the left stick
    //   move the selection (GamepadNavigation), A submits, B cancels. A substituted reader drives the UI too, and the
    //   d-pad works on every platform (its axes differ). The keyboard does not navigate: the menus never had it, and
    //   any key takes the scheme to KeyboardMouse, which drops the selection.
    // - In the Gamepad scheme the mouse is reported far off the screen: what it hovered gets its pointer exit, so a
    //   hover and a selection are never seen at once. A held mouse button gives the real position back at once (the
    //   click takes the scheme back to KeyboardMouse).
    // - Everything else (the mouse in other schemes, touches, the composition of the IME) is the base Input.
    public class GamepadUIInput : BaseInput
    {
        private static readonly Vector2 OffScreen = new(-100000f, -100000f);

        private IInputScheme _scheme;
        private IGamepadReader _gamepad;
        private InputOptions _options;
        private StandaloneInputModule _module;

        private int _suppressedSubmitFrame = -1;
        private bool _isMoveSuppressed;

        public override Vector2 mousePosition => IsMouseHidden() == true ? OffScreen : base.mousePosition;

        public void Construct(IInputScheme scheme, IGamepadReader gamepad, InputOptions options, StandaloneInputModule module)
        {
            _scheme = scheme;
            _gamepad = gamepad;
            _options = options;
            _module = module;

            module.inputOverride = this;
        }

        // The press that has just put the selection on a button (a window opened, the scheme switched) must not press
        // it too: the submit of this frame and of the next one is dropped (the module may have read this frame before
        // the selection was made), the move - until the direction is let go.
        public void SuppressPress()
        {
            _suppressedSubmitFrame = Time.frameCount;
            _isMoveSuppressed = true;
        }

        public override float GetAxisRaw(string axisName)
        {
            if (axisName == _module.horizontalAxis)
                return GetMove().x;

            if (axisName == _module.verticalAxis)
                return GetMove().y;

            return base.GetAxisRaw(axisName);
        }

        public override bool GetButtonDown(string buttonName)
        {
            if (buttonName == _module.submitButton)
                return Time.frameCount - _suppressedSubmitFrame > 1 && _gamepad.WasPressed(GamepadButton.A) == true;

            if (buttonName == _module.cancelButton)
                return _gamepad.WasPressed(GamepadButton.B);

            return base.GetButtonDown(buttonName);
        }

        private Vector2 GetMove()
        {
            var direction = GamepadNavigation.GetDirection(_gamepad, _options);

            if (direction == Vector2Int.zero)
                _isMoveSuppressed = false;

            if (_isMoveSuppressed == true)
                return Vector2.zero;

            return direction;
        }

        private bool IsMouseHidden()
        {
            if (_scheme.Current.Value != InputSchemeType.Gamepad)
                return false;

            return Input.GetMouseButton(0) == false && Input.GetMouseButton(1) == false && Input.GetMouseButton(2) == false;
        }
    }
}
