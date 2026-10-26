using System;
using Internal;

namespace Global.Audio
{
    // A line of sounds with a volume of its own, multiplied by the master one. The framework has Music and SFX; a game
    // declares its own lines once, as static fields, with the volume read from its IAudioVolume:
    //     public static readonly AudioLine Dialogue = AudioLine.Of<IGameVolume>("Dialogue", v => v.DialogueVolume);
    // A sound goes to a line with SoundPlay.WithLine; the music always plays on Music. Lines are compared by reference.
    public sealed class AudioLine
    {
        private AudioLine(string name, Func<IAudioVolume, IViewableProperty<float>> volume)
        {
            Name = name;
            _volume = volume;
        }

        public static readonly AudioLine Music = new("Music", volume => volume.MusicVolume);
        public static readonly AudioLine SFX = new("SFX", volume => volume.SoundsVolume);

        private readonly Func<IAudioVolume, IViewableProperty<float>> _volume;

        public string Name { get; }

        // A line of the game. The IAudioVolume of the scope must be a TVolume, otherwise the first sound of the line
        // throws.
        public static AudioLine Of<TVolume>(string name, Func<TVolume, IViewableProperty<float>> volume)
            where TVolume : IAudioVolume
        {
            return new AudioLine(name, source => {
                if (source is TVolume typed)
                    return volume(typed);

                throw new InvalidOperationException(
                    $"[Audio] Line {name} needs an IAudioVolume of type {typeof(TVolume).Name}, the scope has {source.GetType().Name}");
            });
        }

        // The volume of the line before the master one.
        public IViewableProperty<float> VolumeOf(IAudioVolume volume)
        {
            return _volume(volume);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
