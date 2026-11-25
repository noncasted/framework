using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public abstract class UpdateProgressionBase : IUpdateProgression
    {
        public UpdateProgressionBase(IReadOnlyLifetime lifetime, IUpdater updater, float time, Action<float> callback)
        {
            if (time < 0f)
                throw new ArgumentOutOfRangeException(nameof(time), time, "The time of a progression is negative");

            _lifetime = lifetime.Child();
            _updater = updater;
            _targetTime = time;
            _callback = callback;

            _completion = new UniTaskCompletionSource();
        }

        private readonly IUpdater _updater;
        private readonly Action<float> _callback;
        private readonly ILifetime _lifetime;
        private readonly UniTaskCompletionSource _completion;
        private readonly float _targetTime;

        private float _currentTime;

        public async UniTask Process()
        {
            Setup(_lifetime, _updater);
            _lifetime.Listen(Dispose);
            await _completion.Task;
        }

        public void PassDelta(float delta)
        {
            _currentTime += delta;

            // A progression of zero time is done on its first update: the division would give NaN and never reach 1.
            // A long last frame passes the end of the time: the callback gets exactly 1 there, never more.
            var progress = _targetTime > 0f ? Math.Min(_currentTime / _targetTime, 1f) : 1f;

            // The error ends the wait: repeated each frame it would never reach the end of the time.
            try
            {
                _callback?.Invoke(progress);
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
                _lifetime.Terminate();
                return;
            }

            if (progress >= 1f)
            {
                _completion.TrySetResult();
                _lifetime.Terminate();
            }
        }

        private void Dispose()
        {
            _completion.TrySetCanceled();
        }

        protected abstract void Setup(IReadOnlyLifetime lifetime, IUpdater updater);
    }
}
