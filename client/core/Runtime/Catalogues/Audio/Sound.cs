using UnityEngine;

namespace Internal
{
    public sealed class Sound
    {
        public Sound(AudioClip clip, float volume, AudioGroupAsset source, string name)
        {
            Clip = clip;
            _volume = volume;
            _source = source;
            _name = name;
        }

        private readonly float _volume;
        private readonly AudioGroupAsset _source;
        private readonly string _name;

        public AudioClip Clip { get; }

        // Volume of the clip itself from the catalogue; the line volume is applied on top of it.
        // In the editor it is read from the group asset, so a slider change is heard without reloading the group.
#if UNITY_EDITOR
        public float Volume => _source != null ? _source.GetVolume(_name, _volume) : _volume;
#else
        public float Volume => _volume;
#endif
    }
}
