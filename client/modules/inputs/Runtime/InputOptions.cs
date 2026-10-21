using Internal;
using UnityEngine;

namespace Global.Inputs
{
    // Options of the input module, registered by AddInput. The controls of a game (aim, dead zones of its sticks,
    // touch layout) live in options of the game.
    public class InputOptions : EnvAsset
    {
        // InputScheme: the mouse moves this many screen pixels away from where it stood before it takes the scheme
        // back to KeyboardMouse; a stick deflected further than this (0..1) takes it to Gamepad.
        [SerializeField, Min(0f)] private float _schemeMouseDistance = 12f;
        [SerializeField, Range(0f, 1f)] private float _schemeStickDeflection = 0.5f;

        // GamepadReader: a trigger (0..1) pressed further than this is a pressed button.
        [SerializeField, Range(0f, 1f)] private float _triggerPressThreshold = 0.5f;

        // UI navigation with a gamepad (GamepadUIInput, GamepadNavigationRepeat): the left stick deflected further
        // than this (0..1) is a step of the selection, as the d-pad. A held direction repeats the step after the delay
        // and then every interval (real seconds).
        [SerializeField, Range(0.1f, 0.95f)] private float _navigationDeflection = 0.5f;
        [SerializeField, Min(0.05f)] private float _navigationRepeatDelay = 0.4f;
        [SerializeField, Min(0.02f)] private float _navigationRepeatInterval = 0.12f;

        public float SchemeMouseDistance => _schemeMouseDistance;
        public float SchemeStickDeflection => _schemeStickDeflection;
        public float TriggerPressThreshold => _triggerPressThreshold;
        public float NavigationDeflection => _navigationDeflection;
        public float NavigationRepeatDelay => _navigationRepeatDelay;
        public float NavigationRepeatInterval => _navigationRepeatInterval;
    }
}
