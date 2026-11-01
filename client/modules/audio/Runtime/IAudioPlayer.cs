using Internal;

namespace Global.Audio
{
    public interface IAudioPlayer
    {
        void PlaySound(Sound sound);

        // One shot with its volume, pitch, pan, limit, line and trim. Takes a free source of the pool; with none free it
        // cuts the oldest sound and logs a warning. Over play.Limit it cuts the oldest sound of the same limit instead.
        // A clip outside the catalogue (loaded at runtime) comes as clip.ToSound().
        void PlaySound(Sound sound, in SoundPlay play);

        // The same, and the sound is cut at once when the lifetime terminates before the clip ends (a reload cut off
        // by a weapon switch). The lifetime should be short: it keeps a listener until it terminates.
        void PlaySound(Sound sound, in SoundPlay play, IReadOnlyLifetime lifetime);

        // Loops the track. The same track that already plays goes on (the menu calls it on every entry);
        // another one starts from its beginning and rises for fadeIn seconds while the old one fades out
        // over the same time (crossfade on two sources).
        void PlayLoopMusic(Sound sound, float fadeIn);

        // The same, and the track fades out for fadeIn seconds when the lifetime terminates while it is still the
        // current one (the theme of an opened book). A later request takes the track over: a request of the same track
        // that goes on, another track, StopMusic - the lifetime does nothing then.
        void PlayLoopMusic(Sound sound, float fadeIn, IReadOnlyLifetime lifetime);

        // Fades out all music for fadeOut seconds and stops it; 0 - at once.
        void StopMusic(float fadeOut);
    }
}
