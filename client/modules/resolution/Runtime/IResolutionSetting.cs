using Internal;

namespace Global.Resolution
{
    // The render resolution the player picked, from the settings of the game (RenderResolution follows it). The settings of
    // the game implement it and register it with .As<IResolutionSetting>(), as they do IAudioVolume.
    public interface IResolutionSetting
    {
        IViewableProperty<ResolutionMode> Resolution { get; }
    }
}
