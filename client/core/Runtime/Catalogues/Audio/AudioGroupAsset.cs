using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
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
            var tasks = new List<UniTask<Exception>>();

            foreach (var entry in _entries)
            {
                if (entry.Encoded != null)
                    tasks.Add(DecodeEntry(entry, decoded));
            }

            _decoded = decoded;

            // WhenAll ends at the first failure and the others would go on writing clips nobody destroys: every
            // decode is waited for, and a failure leaves no clip behind.
            var failures = await UniTask.WhenAll(tasks);
            var failure = Array.Find(failures, exception => exception != null);

            if (failure == null)
                return;

            ReleaseDecoded();
            ExceptionDispatchInfo.Capture(failure).Throw();
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

        private static async UniTask<Exception> DecodeEntry(AudioAssetEntry entry, Dictionary<string, AudioClip> decoded)
        {
            try
            {
                decoded.Add(entry.Name, await EncodedAudioDecoder.Decode(entry.Encoded, entry.Name));
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
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

            entry.SetVolume(volume);
            return true;
        }
#endif

        private AudioAssetEntry FindEntry(string name)
        {
            return GroupEntries.Find(_entries, name, static entry => entry.Name);
        }
    }
}
