using System;
using Cysharp.Threading.Tasks;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Internal
{
    // The decoder of the clips an audio group ships as mp3 bytes (TextAsset "*.mp3.bytes", see AudioAssetEntry.Encoded).
    // It lives in the assembly Internal.EncodedAudio, which registers itself at the start: the core does not reference
    // UnityWebRequest, so a game that ships no mp3 bytes drops that assembly from the player and the module with it.
    public interface IEncodedAudioDecoder
    {
        UniTask<AudioClip> Decode(TextAsset encoded, string name);
    }

    [AutoStaticsCleanup]
    public static partial class EncodedAudioDecoder
    {
        public const string Extension = ".mp3.bytes";

        private static IEncodedAudioDecoder _current;

        public static void Use(IEncodedAudioDecoder decoder)
        {
            _current = decoder;
        }

        public static UniTask<AudioClip> Decode(TextAsset encoded, string name)
        {
            if (_current == null)
                throw new InvalidOperationException(
                    $"[EncodedAudio] {name} is shipped as mp3 bytes, but the player has no decoder: Internal.EncodedAudio is " +
                    "listed in StrippedPlayerAssemblies of ProjectSettings/Framework.json");

            return _current.Decode(encoded, name);
        }
    }
}
