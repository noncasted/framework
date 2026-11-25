using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Internal
{
    // The YAML of the games holds the old keys: FormerlySerializedAs stays until every game has re-saved its assets.
    // The keys are for SerializedProperty access from the generators and the tests.
    [Serializable]
    public sealed class AudioAssetEntry
    {
        public const string NameKey = nameof(_name);
        public const string ClipKey = nameof(_clip);
        public const string EncodedKey = nameof(_encoded);
        public const string VolumeKey = nameof(_volume);

        [SerializeField] [FormerlySerializedAs("Name")] private string _name;
        [SerializeField] [FormerlySerializedAs("Clip")] private AudioClip _clip;

        // Set instead of Clip for clips shipped as mp3 bytes (EncodedAudioDecoder): decoded by Decode() on group load.
        [SerializeField] [FormerlySerializedAs("Encoded")] private TextAsset _encoded;

        [SerializeField] [FormerlySerializedAs("Volume")] [Range(0f, 1f)] private float _volume = 1f;

        // The serializer of Unity creates an entry through this one, which keeps the initial volume.
        public AudioAssetEntry()
        {
        }

        public AudioAssetEntry(string name, AudioClip clip, TextAsset encoded, float volume)
        {
            _name = name;
            _clip = clip;
            _encoded = encoded;
            _volume = volume;
        }

        public string Name => _name;
        public AudioClip Clip => _clip;
        public TextAsset Encoded => _encoded;
        public float Volume => _volume;

#if UNITY_EDITOR
        internal void SetVolume(float volume)
        {
            _volume = volume;
        }
#endif
    }
}
