namespace Global.Inputs
{
    // Buttons of a gamepad by meaning, named as on Xbox (the W3C standard gamepad layout). The triggers and the
    // directions of the d-pad are buttons too: a trigger is pressed beyond InputOptions.TriggerPressThreshold.
    public enum GamepadButton
    {
        A,
        B,
        X,
        Y,
        LeftBumper,
        RightBumper,
        LeftTrigger,
        RightTrigger,
        View,
        Menu,
        LeftStick,
        RightStick,
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight
    }
}
