using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class UpdatableActionExtensions
    {
        public static UniTask RunFixedAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Func<bool> predicate,
            Action<float> callback)
        {
            var action = new FixedUpdatableActions(lifetime, updater, callback, predicate);
            return action.Process();
        }

        public static UniTask RunUpdateAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Func<bool> predicate,
            Action<float> callback)
        {
            var action = new UpdatableAction(lifetime, updater, callback, predicate);
            return action.Process();
        }

        public static UniTask RunUpdateAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Action<float> callback)
        {
            var action = new UpdatableAction(lifetime, updater, callback, () => true);
            return action.Process();
        }

        // The actions run until the predicate fails; the end of the lifetime cancels them (OperationCanceledException).

        // Waits the time of the updater (the sum of its deltas: a paused updater does not move it).
        public static UniTask RunDelay(this IUpdater updater, IReadOnlyLifetime lifetime, float time)
        {
            var passed = 0f;

            var action = new UpdatableAction(lifetime, updater, delta => passed += delta, () => passed < time);
            return action.Process();
        }

        public static UniTask RunUpdateAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            float time,
            Action<float> callback)
        {
            // The time of the updater, as RunDelay: Time.time goes on while the updater is paused or slowed down.
            var passed = 0f;

            var action = new UpdatableAction(lifetime, updater, Callback, () => passed < time);
            return action.Process();

            void Callback(float delta)
            {
                passed += delta;
                callback.Invoke(delta);
            }
        }
    }
}