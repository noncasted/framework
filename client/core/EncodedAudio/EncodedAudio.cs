using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.IO;
#endif

namespace Internal
{
    // For WebGL Unity re-encodes every AudioClip to AAC of at least ~100 kbps whatever the import settings say
    // (force to mono, sample rate and quality below 0.3 change nothing), so long or numerous clips ship as ready
    // mp3 bytes (TextAsset "*.mp3.bytes") and are decoded at load time: by the browser (decodeAudioData) in
    // WebGL, by FMOD in the editor and other players. The result is a regular fully decoded AudioClip. Registered as
    // the decoder of EncodedAudioDecoder at the start (before the first scene).
    public sealed class EncodedAudio : IEncodedAudioDecoder
    {
        private const float DecodeTimeout = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            EncodedAudioDecoder.Use(new EncodedAudio());
        }

        public async UniTask<AudioClip> Decode(TextAsset encoded, string name)
        {
            var url = CreateUrl(encoded.bytes, name);

            try
            {
                using var request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
                await request.SendWebRequest().ToUniTask();

                var clip = DownloadHandlerAudioClip.GetContent(request);
                clip.name = name;

                // WebGL decodes after the download ends (decodeAudioData) and reports Unloaded until it is done:
                // a clip played before that stays silent. Elsewhere the clip arrives already Loaded.
                var deadline = Time.realtimeSinceStartup + DecodeTimeout;

                while (clip.loadState == AudioDataLoadState.Unloaded || clip.loadState == AudioDataLoadState.Loading)
                {
                    if (Time.realtimeSinceStartup > deadline)
                        throw new TimeoutException($"[EncodedAudio] {name} is not decoded in {DecodeTimeout} s");

                    await UniTask.Yield();
                }

                if (clip.loadState != AudioDataLoadState.Loaded)
                    throw new InvalidOperationException($"[EncodedAudio] Failed to decode {name}: {clip.loadState}");

                return clip;
            }
            finally
            {
                RevokeUrl(url);
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static string CreateUrl(byte[] bytes, string name)
        {
            return EncodedAudioCreateUrl(bytes, bytes.Length);
        }

        private static void RevokeUrl(string url)
        {
            EncodedAudioRevokeUrl(url);
        }

        [DllImport("__Internal")]
        private static extern string EncodedAudioCreateUrl(byte[] bytes, int length);

        [DllImport("__Internal")]
        private static extern void EncodedAudioRevokeUrl(string url);
#else
        // FMOD reads a file, so the bytes go through a temporary one; the decoded clip no longer needs it.
        private static string CreateUrl(byte[] bytes, string name)
        {
            var folder = Path.Combine(Application.temporaryCachePath, "EncodedAudio");
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"{name}_{Guid.NewGuid():N}.mp3");
            File.WriteAllBytes(path, bytes);
            return new Uri(path).AbsoluteUri;
        }

        private static void RevokeUrl(string url)
        {
            File.Delete(new Uri(url).LocalPath);
        }
#endif
    }
}
