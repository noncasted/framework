using System;
using UnityEngine;

namespace Global.Audio
{
    // How a kind of sounds is played, set in options. Volume and pitch get a random spread on every play; the instance
    // of the style is the kind for the limit: every sound played with one style shares its cap (all enemy steps).
    [Serializable]
    public class SoundStyle
    {
        [SerializeField, Min(0f)] private float _volume = 1f;

        // Volume * (1 ± spread).
        [SerializeField, Range(0f, 1f)] private float _volumeSpread;

        [SerializeField, Min(0.01f)] private float _pitch = 1f;

        // Pitch * (1 ± spread).
        [SerializeField, Range(0f, 0.5f)] private float _pitchSpread;

        // Instances of this style that sound at once; 0 - no limit.
        [SerializeField, Min(0)] private int _maxInstances;

        [NonSerialized] private SoundLimit _limit;

        public SoundStyle()
        {
        }

        public SoundStyle(float volume, float volumeSpread, float pitchSpread, int maxInstances)
        {
            _volume = volume;
            _volumeSpread = volumeSpread;
            _pitchSpread = pitchSpread;
            _maxInstances = maxInstances;
        }

        // A kind played higher or lower than its clip (the coins over the key clip, the enemy death under the hit).
        public SoundStyle(float volume, float volumeSpread, float pitch, float pitchSpread, int maxInstances)
            : this(volume, volumeSpread, pitchSpread, maxInstances)
        {
            _pitch = pitch;
        }

        public float Volume => _volume;
        public float VolumeSpread => _volumeSpread;
        public float Pitch => _pitch;
        public float PitchSpread => _pitchSpread;
        public int MaxInstances => _maxInstances;

        // Made once per style instance; the cap read then stays until the domain reload.
        public SoundLimit Limit
        {
            get
            {
                if (_maxInstances == 0)
                    return null;

                _limit ??= new SoundLimit(_maxInstances);
                return _limit;
            }
        }
    }
}
