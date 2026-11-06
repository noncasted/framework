using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Measurement
{
    /// <summary> Bodies falling onto a static ground: the 2D physics a game keeps (Physics2D, Box2D). </summary>
    public sealed class PhysicsPart : MonoBehaviour, IMeasurementPart
    {
        private const int Count = 16;
        private const float StartHeight = 4f;

        [SerializeField] private Sprite _sprite;

        public string Name => "physics";

        public void Init(Sprite sprite)
        {
            _sprite = sprite;
        }

        public async UniTask<string> Run(CancellationToken cancellation)
        {
            var ground = new GameObject("Ground");
            ground.transform.SetParent(transform, false);
            ground.transform.localPosition = new Vector3(0f, -4f, 0f);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(20f, 1f);

            var bodies = new List<Rigidbody2D>();

            for (var i = 0; i < Count; i++)
            {
                var item = new GameObject($"Body {i}");
                item.transform.SetParent(transform, false);
                item.transform.localPosition = new Vector3(i - Count / 2f, StartHeight + i % 3, 0f);
                item.AddComponent<SpriteRenderer>().sprite = _sprite;
                item.AddComponent<CircleCollider2D>().radius = 0.5f;
                bodies.Add(item.AddComponent<Rigidbody2D>());
            }

            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: cancellation);

            var fallen = 0;

            foreach (var body in bodies)
            {
                if (body.position.y < StartHeight)
                    fallen++;
            }

            if (fallen == 0)
                throw new InvalidOperationException("No body moved: 2D physics does not run");

            return $"{fallen} of {Count} bodies fell";
        }
    }
}
