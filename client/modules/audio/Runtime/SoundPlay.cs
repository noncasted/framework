using System;

namespace Global.Audio
{
    // How one sound is played on top of the clip volume of the catalog and the SFX line.
    public readonly struct SoundPlay
    {
        public SoundPlay(float volume, float pitch, float pan, SoundLimit limit)
        {
            if (pitch <= 0f)
                throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "Must be positive");

            Volume = volume;
            Pitch = pitch;
            Pan = pan;
            Limit = limit;
        }

        public static SoundPlay Default => new(1f, 1f, 0f, null);

        // Multiplier of the clip volume.
        public float Volume { get; }

        // 1 - as recorded; also the speed of the clip.
        public float Pitch { get; }

        // AudioSource.panStereo: -1 left .. 1 right.
        public float Pan { get; }

        // Instances of one kind that may sound at once; null - no limit.
        public SoundLimit Limit { get; }
    }

    // A kind of sounds with a cap on instances that sound at once: a new one over the cap cuts the oldest one of the
    // same kind. The kind is the instance of this class.
    public sealed class SoundLimit
    {
        public SoundLimit(int max)
        {
            if (max < 1)
                throw new ArgumentOutOfRangeException(nameof(max), max, "Must be at least 1");

            Max = max;
        }

        public int Max { get; }
    }
}
