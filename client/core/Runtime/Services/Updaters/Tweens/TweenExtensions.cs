using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // Tweens over the time of the updater. Each lives while the given lifetime lives: its termination stops the
    // tween at the current value and cancels the task. A tween nobody awaits is started with NoAwait().
    public static class TweenExtensions
    {
        // callback gets the eased share of the way: 0 at the start, exactly 1 on the last call.
        public static async UniTask Tween(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            float time,
            EaseType ease,
            Action<float> callback)
        {
            if (time <= 0f)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                callback.Invoke(Easing.Evaluate(ease, 1f));
                return;
            }

            await updater.Progression(lifetime, time, Callback);

            void Callback(float progress)
            {
                callback.Invoke(Easing.Evaluate(ease, Mathf.Clamp01(progress)));
            }
        }

        public static async UniTask Tween(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            float time,
            AnimationCurve curve,
            Action<float> callback)
        {
            if (time <= 0f)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                callback.Invoke(curve.Evaluate(1f));
                return;
            }

            await updater.Progression(lifetime, time, Callback);

            void Callback(float progress)
            {
                callback.Invoke(curve.Evaluate(Mathf.Clamp01(progress)));
            }
        }

        public static UniTask Move(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Transform target,
            Vector3 position,
            float time,
            EaseType ease = EaseType.OutQuad)
        {
            var start = target.position;

            return updater.Tween(lifetime, time, ease,
                factor => target.position = Vector3.LerpUnclamped(start, position, factor));
        }

        // A tween of one axis does not touch the others, so X and Y can run in parallel.
        public static UniTask MoveX(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Transform target,
            float x,
            float time,
            EaseType ease = EaseType.OutQuad)
        {
            var start = target.position.x;

            return updater.Tween(lifetime, time, ease, Apply);

            void Apply(float factor)
            {
                var position = target.position;
                position.x = Mathf.LerpUnclamped(start, x, factor);
                target.position = position;
            }
        }

        public static UniTask MoveY(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Transform target,
            float y,
            float time,
            EaseType ease = EaseType.OutQuad)
        {
            var start = target.position.y;

            return updater.Tween(lifetime, time, ease, Apply);

            void Apply(float factor)
            {
                var position = target.position;
                position.y = Mathf.LerpUnclamped(start, y, factor);
                target.position = position;
            }
        }

        public static UniTask LocalMoveX(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Transform target,
            float x,
            float time,
            EaseType ease = EaseType.OutQuad)
        {
            var start = target.localPosition.x;

            return updater.Tween(lifetime, time, ease, Apply);

            void Apply(float factor)
            {
                var position = target.localPosition;
                position.x = Mathf.LerpUnclamped(start, x, factor);
                target.localPosition = position;
            }
        }

        public static UniTask MoveAnchored(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            RectTransform target,
            Vector2 position,
            float time,
            EaseType ease = EaseType.OutQuad)
        {
            var start = target.anchoredPosition;

            return updater.Tween(lifetime, time, ease,
                factor => target.anchoredPosition = Vector2.LerpUnclamped(start, position, factor));
        }

        // The rotation takes the shortest arc.
        public static UniTask Rotate(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Transform target,
            Quaternion rotation,
            float time,
            EaseType ease = EaseType.OutQuad)
        {
            var start = target.rotation;

            return updater.Tween(lifetime, time, ease,
                factor => target.rotation = Quaternion.SlerpUnclamped(start, rotation, factor));
        }

        public static UniTask LocalRotate(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Transform target,
            Quaternion rotation,
            float time,
            EaseType ease = EaseType.OutQuad)
        {
            var start = target.localRotation;

            return updater.Tween(lifetime, time, ease,
                factor => target.localRotation = Quaternion.SlerpUnclamped(start, rotation, factor));
        }

        public static UniTask Scale(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Transform target,
            Vector3 scale,
            float time,
            EaseType ease = EaseType.OutQuad)
        {
            var start = target.localScale;

            return updater.Tween(lifetime, time, ease,
                factor => target.localScale = Vector3.LerpUnclamped(start, scale, factor));
        }

        // A shake around the start position: random offsets within strength per axis fade out towards the end, on
        // the last frame the target returns to its place. vibrato is the number of offsets per second.
        public static UniTask ShakePosition(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Transform target,
            float time,
            Vector3 strength,
            int vibrato)
        {
            var start = target.position;
            var count = Mathf.Max(2, Mathf.CeilToInt(vibrato * time));
            var offsets = new Vector3[count + 1];

            for (var i = 1; i < count; i++)
            {
                var fade = 1f - (float)i / count;
                var direction = UnityEngine.Random.insideUnitCircle.normalized;
                offsets[i] = new Vector3(direction.x * strength.x, direction.y * strength.y, 0f) * fade;
            }

            return updater.Tween(lifetime, time, EaseType.Linear, Apply);

            void Apply(float factor)
            {
                var scaled = factor * count;
                var segment = Mathf.Min(Mathf.FloorToInt(scaled), count - 1);
                var offset = Vector3.Lerp(offsets[segment], offsets[segment + 1], scaled - segment);
                target.position = start + offset;
            }
        }
    }
}
