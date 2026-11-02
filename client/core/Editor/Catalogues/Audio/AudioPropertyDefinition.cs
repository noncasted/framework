using UnityEngine;

namespace Internal
{
    internal sealed class AudioPropertyDefinition
    {
        public AudioPropertyDefinition(
            string propertyName,
            string fieldName,
            VariationKey variation,
            AudioClip clip,
            TextAsset encoded,
            float volume)
        {
            PropertyName = propertyName;
            FieldName = fieldName;
            Variation = variation;
            Clip = clip;
            Encoded = encoded;
            Volume = volume;
        }

        public string PropertyName { get; }
        public string FieldName { get; }
        public VariationKey Variation { get; }
        public AudioClip Clip { get; }
        public TextAsset Encoded { get; }
        public float Volume { get; }
    }
}
