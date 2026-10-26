using System;

namespace Global.Audio
{
    // How one sound is played on top of the clip volume of the catalog and the volume of its line (SFX by default).
    public readonly struct SoundPlay
    {
        public SoundPlay(float volume, float pitch, float pan, SoundLimit limit)
            : this(volume, pitch, pan, limit, AudioLine.SFX, 0f, 0f)
        {
        }

        private SoundPlay(float volume, float pitch, float pan, SoundLimit limit, AudioLine line, float startOffset,
            float endOffset)
        {
            if (pitch <= 0f)
                throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "Must be positive");

            if (line == null)
                throw new ArgumentNullException(nameof(line));

            if (startOffset < 0f)
                throw new ArgumentOutOfRangeException(nameof(startOffset), startOffset, "Must not be negative");

            if (endOffset < 0f)
                throw new ArgumentOutOfRangeException(nameof(endOffset), endOffset, "Must not be negative");

            Volume = volume;
            Pitch = pitch;
            Pan = pan;
            Limit = limit;
            Line = line;
            StartOffset = startOffset;
            EndOffset = endOffset;
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

        // The line whose volume the sound follows.
        public AudioLine Line { get; }

        // Seconds of the clip skipped at its beginning.
        public float StartOffset { get; }

        // Seconds of the clip cut at its end: the sound stops there.
        public float EndOffset { get; }

        // The same play on another line (the dialogue of the game).
        public SoundPlay WithLine(AudioLine line)
        {
            return new SoundPlay(Volume, Pitch, Pan, Limit, line, StartOffset, EndOffset);
        }

        // The same play of a part of the clip (a fragment of a longer recording): startOffset seconds skipped at its
        // beginning, endOffset seconds cut at its end. Both are in the time of the clip, whatever the pitch.
        public SoundPlay WithTrim(float startOffset, float endOffset)
        {
            return new SoundPlay(Volume, Pitch, Pan, Limit, Line, startOffset, endOffset);
        }
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
