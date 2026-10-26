using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public abstract class UpdatableActionBase : IUpdatableAction
    {
        protected UpdatableActionBase(
            IReadOnlyLifetime lifetime,
            IUpdater updater,
            Action<float> callback,
            Func<bool> predicate)
        {
            _lifetime = lifetime.Child();
            _updater = updater;
            _callback = callback;
            _predicate = predicate;

            _completion = new UniTaskCompletionSource();
        }

        private readonly IUpdater _updater;
        private readonly Action<float> _callback;
        private readonly Func<bool> _predicate;
        private readonly ILifetime _lifetime;
        private readonly UniTaskCompletionSource _completion;

        public async UniTask Process()
        {
            ListenUpdate(_lifetime, _updater);
            _lifetime.Listen(Dispose);
            await _completion.Task;
        }

        protected void PassDelta(float delta)
        {
            if (_predicate.Invoke() == false)
            {
                _completion.TrySetResult();
                _lifetime.Terminate();
                return;
            }

            _callback?.Invoke(delta);
        }

        // The lifetime ended before the predicate: the wait is cancelled, as Progression and Tween are. After the
        // completion by the predicate the cancellation is ignored.
        private void Dispose()
        {
            _completion.TrySetCanceled();
        }

        protected abstract void ListenUpdate(IReadOnlyLifetime lifetime, IUpdater updater);
    }
}