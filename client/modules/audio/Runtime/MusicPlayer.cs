using System;
using Internal;
using UnityEngine;

namespace Global.Audio
{
    // The looped tracks on two channels: a new track rises on the free one while the old one fades out.
    internal sealed class MusicPlayer
    {
        public MusicPlayer(AudioSource[] sources, AudioLineVolumes volumes)
        {
            // Crossfade needs exactly two sources: the rising track and the fading one.
            if (sources.Length != 2)
                throw new InvalidOperationException($"AudioPlayer needs 2 music sources, has {sources.Length}");

            _volumes = volumes;
            _channels = new MusicChannel[sources.Length];

            for (var i = 0; i < _channels.Length; i++)
                _channels[i] = new MusicChannel(sources[i]);
        }

        private readonly AudioLineVolumes _volumes;
        private readonly MusicChannel[] _channels;

        private int _active;
        private long _order;

        public void Play(Sound sound, float fadeIn)
        {
            var active = _channels[_active];

            // The menu requests music on every entry: an already playing track is not restarted, the request takes it
            // over.
            if (active.IsPlaying(sound) == true)
            {
                active.TakeOver(++_order);
                return;
            }

            // The old track fades out over the same time the new one rises; one already fading keeps its pace.
            if (active.IsRising == true)
                active.FadeTo(0f, fadeIn);

            // The other source is free or still fades out an older track: that one is cut.
            _active = (_active + 1) % _channels.Length;
            _channels[_active].Start(sound, fadeIn);
            _channels[_active].TakeOver(++_order);

            ApplyVolume();
        }

        public void Play(Sound sound, float fadeIn, IReadOnlyLifetime lifetime)
        {
            Play(sound, fadeIn);

            var channel = _channels[_active];
            var order = channel.Order;

            lifetime.Listen(() => {
                if (channel.Order != order || channel.IsRising == false)
                    return;

                channel.FadeTo(0f, fadeIn);
                ApplyVolume();
            });
        }

        public void Stop(float fadeOut)
        {
            foreach (var channel in _channels)
                channel.FadeTo(0f, fadeOut);

            ApplyVolume();
        }

        public void Step(float delta)
        {
            var changed = false;

            foreach (var channel in _channels)
                changed |= channel.Step(delta);

            if (changed == true)
                ApplyVolume();
        }

        public void ApplyVolume()
        {
            foreach (var channel in _channels)
                channel.ApplyVolume(_volumes.Of(AudioLine.Music));
        }
    }
}
