namespace Global.Resolution
{
    // The render resolution of a phone or a tablet the player picks in the settings (IResolutionSetting). Auto is the first
    // value: a save of the game written before the setting existed gets it. Low is the whole upscale of render-resolution.js
    // (a canvas pixel is k device pixels), High the device pixel ratio; Auto is High on Android and Low on iOS and iPadOS
    // (their GPUs are the limit already at the low one). A desktop keeps the ratio of the browser whatever is picked.
    public enum ResolutionMode
    {
        Auto = 0,
        Low = 1,
        High = 2
    }
}
