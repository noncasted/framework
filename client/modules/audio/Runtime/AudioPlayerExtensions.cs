using System;
using System.Collections.Generic;
using Internal;

namespace Global.Audio
{
    public static class AudioPlayerExtensions
    {
        // Which variation sounds is cosmetic and local: a local seed, no UnityEngine.Random.
        private static readonly Random _random = new(Environment.TickCount);

        // Variations of one sound from the catalogue: a random one is taken so repeats do not sound the same.
        public static void PlayRandomFromGroup(this IAudioPlayer player, IReadOnlyList<Sound> group)
        {
            player.PlaySound(group[_random.Next(group.Count)]);
        }
    }
}
