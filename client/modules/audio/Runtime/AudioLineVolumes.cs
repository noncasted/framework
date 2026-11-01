using System;
using System.Collections.Generic;
using Internal;

namespace Global.Audio
{
    // The volume of each followed line with the master one. Music and SFX are followed from the start, a line of the
    // game from its first sound.
    internal sealed class AudioLineVolumes
    {
        public AudioLineVolumes(IAudioVolume volume, IReadOnlyLifetime lifetime, Action changed)
        {
            _volume = volume;
            _lifetime = lifetime;
            _changed = changed;
        }

        private readonly IAudioVolume _volume;
        private readonly IReadOnlyLifetime _lifetime;
        private readonly Action _changed;

        private readonly Dictionary<AudioLine, IViewableProperty<float>> _lines = new();
        private readonly Dictionary<AudioLine, float> _values = new();

        public void Start()
        {
            // Both lines are known before the first view: Update writes every followed line together.
            _lines.Add(AudioLine.Music, AudioLine.Music.VolumeOf(_volume));
            _lines.Add(AudioLine.SFX, AudioLine.SFX.VolumeOf(_volume));

            _volume.MasterVolume.View(_lifetime, Update);

            foreach (var line in _lines.Values)
                line.View(_lifetime, Update);
        }

        public float Of(AudioLine line)
        {
            return _values[line];
        }

        public void Follow(AudioLine line)
        {
            if (_lines.ContainsKey(line) == true)
                return;

            var volume = line.VolumeOf(_volume);
            _lines.Add(line, volume);
            volume.View(_lifetime, Update);
        }

        private void Update()
        {
            var master = _volume.MasterVolume.Value;

            foreach (var (line, volume) in _lines)
                _values[line] = volume.Value * master;

            _changed();
        }
    }
}
