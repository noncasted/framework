using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Measurement
{
    /// <summary> An imported clip played through an audio source: the audio module of the web player. </summary>
    public sealed class AudioPart : MonoBehaviour, IMeasurementPart
    {
        [SerializeField] private AudioClip _clip;

        public string Name => "audio";

        public void Init(AudioClip clip)
        {
            _clip = clip;
        }

        public async UniTask<string> Run(CancellationToken cancellation)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = _clip;
            source.volume = 0.2f;
            source.Play();

            await UniTask.Delay(TimeSpan.FromSeconds(0.2), cancellationToken: cancellation);

            return $"{_clip.length:F2} s clip, playing {source.isPlaying}";
        }
    }
}
