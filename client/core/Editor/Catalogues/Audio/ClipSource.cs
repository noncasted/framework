using UnityEngine;

namespace Internal
{
    internal sealed class ClipSource
    {
        public ClipSource(
            string path,
            string group,
            string propertyName,
            VariationKey variation,
            AudioClip clip,
            TextAsset encoded,
            float volume)
        {
            Path = path;
            Group = group;
            PropertyName = propertyName;
            Variation = variation;
            Clip = clip;
            Encoded = encoded;
            Volume = volume;
        }

        public string Path { get; }
        public string Group { get; }
        public string PropertyName { get; }
        public VariationKey Variation { get; }
        public AudioClip Clip { get; }
        public TextAsset Encoded { get; }
        public float Volume { get; }
    }
}
