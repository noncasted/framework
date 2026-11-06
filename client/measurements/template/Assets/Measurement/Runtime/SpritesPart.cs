using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Measurement
{
    /// <summary> Sprites moved and recoloured every frame: sprite rendering, sorting, transforms. </summary>
    public sealed class SpritesPart : MonoBehaviour, IMeasurementPart
    {
        private const int Count = 64;
        private const int Frames = 30;

        [SerializeField] private Sprite _sprite;

        private readonly List<SpriteRenderer> _renderers = new();
        private int _frames;

        public string Name => "sprites";

        public void Init(Sprite sprite)
        {
            _sprite = sprite;
        }

        public async UniTask<string> Run(CancellationToken cancellation)
        {
            for (var i = 0; i < Count; i++)
            {
                var item = new GameObject($"Sprite {i}");
                item.transform.SetParent(transform, false);

                var renderer = item.AddComponent<SpriteRenderer>();
                renderer.sprite = _sprite;
                renderer.sortingOrder = i;
                _renderers.Add(renderer);
            }

            await UniTask.DelayFrame(Frames, cancellationToken: cancellation);

            return $"{_renderers.Count} renderers, {_frames} frames";
        }

        private void Update()
        {
            if (_renderers.Count == 0)
                return;

            _frames++;
            var time = Time.time;

            for (var i = 0; i < _renderers.Count; i++)
            {
                var angle = time + i * 0.1f;
                _renderers[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * (i % 8), Mathf.Sin(angle) * (i / 8), 0f);
                _renderers[i].color = Color.HSVToRGB(i / (float)Count, 0.6f, 1f);
            }
        }
    }
}
