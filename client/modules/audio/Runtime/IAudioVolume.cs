using Internal;

namespace Global.Audio
{
    // Volumes of the audio lines, 0..1, set by the game (its settings). The player follows them on the fly.
    public interface IAudioVolume
    {
        IViewableProperty<float> MasterVolume { get; }
        IViewableProperty<float> SoundsVolume { get; }
        IViewableProperty<float> MusicVolume { get; }
    }
}
