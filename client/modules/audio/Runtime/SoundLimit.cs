using System;

namespace Global.Audio
{
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
