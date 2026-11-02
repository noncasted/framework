using System;
using System.Collections.Generic;
using Internal;
using UnityEngine;
using Random = System.Random;

namespace Global.Audio
{
    public static class AudioPlayerExtensions
    {
        // Which variation sounds is cosmetic and local: a local seed, no UnityEngine.Random.
        private static readonly Random _random = new(Environment.TickCount);

        // A clip outside the catalogue (loaded at runtime from the content of the game) as a sound of the player;
        // volume is the clip volume the catalogue would give.
        public static Sound ToSound(this AudioClip clip, float volume = 1f)
        {
            return new Sound(clip, volume, null, clip.name);
        }

        // Variations of one sound from the catalogue: a random one is taken so repeats do not sound the same.
        public static void PlayRandomFromGroup(this IAudioPlayer player, IReadOnlyList<Sound> group)
        {
            player.PlaySound(Pick(group));
        }

        // The sound by its style: the volume and the pitch with their random spread, the limit of the style. volume and
        // pitch scale the ones of the style (a remote shot quieter, a pickup ladder higher), pan is panStereo.
        public static void PlaySound(this IAudioPlayer player, Sound sound, SoundStyle style, float pan = 0f,
            float volume = 1f, float pitch = 1f)
        {
            player.PlaySound(sound, ToPlay(style, pan, volume, pitch));
        }

        // The same, cut when the lifetime terminates before the clip ends.
        public static void PlaySound(this IAudioPlayer player, Sound sound, SoundStyle style, IReadOnlyLifetime lifetime,
            float pan = 0f, float volume = 1f, float pitch = 1f)
        {
            player.PlaySound(sound, ToPlay(style, pan, volume, pitch), lifetime);
        }

        // A random variation by the style.
        public static void PlayRandom(this IAudioPlayer player, IReadOnlyList<Sound> group, SoundStyle style,
            float pan = 0f, float volume = 1f, float pitch = 1f)
        {
            player.PlaySound(Pick(group), ToPlay(style, pan, volume, pitch));
        }

        public static Sound Pick(IReadOnlyList<Sound> group)
        {
            if (group.Count == 0)
                throw new ArgumentException("An empty group of sounds", nameof(group));

            return group[_random.Next(group.Count)];
        }

        public static SoundPlay ToPlay(SoundStyle style, float pan = 0f, float volume = 1f, float pitch = 1f)
        {
            var volumeValue = style.Volume * volume * (1f + Spread(style.VolumeSpread));
            var pitchValue = style.Pitch * pitch * (1f + Spread(style.PitchSpread));

            return new SoundPlay(Math.Max(0f, volumeValue), Math.Max(0.01f, pitchValue), Math.Clamp(pan, -1f, 1f),
                style.Limit);
        }

        // -spread..spread.
        private static float Spread(float spread)
        {
            return spread <= 0f ? 0f : (float)(_random.NextDouble() * 2.0 - 1.0) * spread;
        }
    }
}
