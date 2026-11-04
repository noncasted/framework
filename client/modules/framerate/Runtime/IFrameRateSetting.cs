using Internal;

namespace Global.FrameRate
{
    // The frame rate the player picked, from the settings of the game (FrameRateLimit follows it). The settings of the game
    // implement it and register it with .As<IFrameRateSetting>(), as they do IAudioVolume.
    public interface IFrameRateSetting
    {
        IViewableProperty<FrameRateMode> FrameRate { get; }
    }
}
