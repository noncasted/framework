namespace Global.FrameRate
{
    // The frame rate the player picks in the settings (IFrameRateSetting). Auto is the first value: a save of the game written
    // before the setting existed gets it. Low and High are the numbers of AutoFrameRateOptions (30 and 60).
    public enum FrameRateMode
    {
        Auto = 0,
        Low = 1,
        High = 2
    }
}
