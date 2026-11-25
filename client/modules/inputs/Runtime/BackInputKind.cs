namespace Global.Inputs
{
    public enum BackInputKind
    {
        // Close the window on top, go back a step: Esc, gamepad B, gamepad Menu (Start).
        Back,

        // Open the pause (and, as Back, close what is open): Esc, gamepad Menu (Start). Gamepad B is not a pause.
        Pause
    }
}
