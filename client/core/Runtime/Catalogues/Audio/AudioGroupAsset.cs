using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public sealed class AudioAssetEntry
    {
        public string Name;
        public AudioClip Clip;

        // Set instead of Clip for clips shipped as mp3 bytes (EncodedAudio): decoded by Decode() on group load.
        public TextAsset Encoded;

        [Range(0f, 1f)] public float Volume = 1f;
    }

    public sealed class AudioGroupAsset : ScriptableObject
    {
        [SerializeField] private AudioAssetEntry[] _entries = Array.Empty<AudioAssetEntry>();

        // Clips decoded from Encoded entries while the group is loaded.
        [NonSerialized] private Dictionary<string, AudioClip> _decoded;

        public Sound Get(string name)
        {
            var entry = FindEntry(name);

            if (entry == null)
                throw new InvalidOperationException($"Audio clip '{name}' is missing in {this.name}");

            var clip = entry.Encoded != null ? GetDecoded(name) : entry.Clip;

            if (clip == null)
                throw new InvalidOperationException($"Audio clip '{name}' is missing in {this.name}");

            return new Sound(clip, entry.Volume, this, name);
        }

        public async UniTask Decode()
        {
            var decoded = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            var tasks = new List<UniTask>();

            foreach (var entry in _entries)
            {
                if (entry.Encoded != null)
                    tasks.Add(DecodeEntry(entry, decoded));
            }

            _decoded = decoded;
            await UniTask.WhenAll(tasks);
        }

        public void ReleaseDecoded()
        {
            if (_decoded == null)
                return;

            foreach (var clip in _decoded.Values)
                Destroy(clip);

            _decoded = null;
        }

        private AudioClip GetDecoded(string name)
        {
            if (_decoded == null || _decoded.TryGetValue(name, out var clip) == false)
                throw new InvalidOperationException($"Audio clip '{name}' of {this.name} is not decoded");

            return clip;
        }

        private static async UniTask DecodeEntry(AudioAssetEntry entry, Dictionary<string, AudioClip> decoded)
        {
            decoded.Add(entry.Name, await EncodedAudio.Decode(entry.Encoded, entry.Name));
        }

        public float GetVolume(string name, float fallback)
        {
            var entry = FindEntry(name);
            return entry != null ? entry.Volume : fallback;
        }

#if UNITY_EDITOR
        public bool TrySetVolume(string name, float volume)
        {
            var entry = FindEntry(name);

            if (entry == null || Mathf.Approximately(entry.Volume, volume))
                return false;

            entry.Volume = volume;
            return true;
        }
#endif

        private AudioAssetEntry FindEntry(string name)
        {
            if (_entries == null)
                return null;

            for (var i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];

                if (entry == null)
                    continue;

                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }
    }
}
