using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public class UpdateUpdateProgression : UpdateProgressionBase, IUpdatable
    {
        public UpdateUpdateProgression(
            IReadOnlyLifetime lifetime,
            IUpdater updater,
            float time,
            Action<float> callback) : base(lifetime, updater, time, callback)
        {
        }

        protected override void Setup(IReadOnlyLifetime lifetime, IUpdater updater)
        {
            updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            PassDelta(delta);
        }
    }

    public class FixedUpdateProgression : UpdateProgressionBase, IFixedUpdatable
    {
        public FixedUpdateProgression(
            IReadOnlyLifetime lifetime,
            IUpdater updater,
            float time,
            Action<float> callback) : base(lifetime, updater, time, callback)
        {
        }

        protected override void Setup(IReadOnlyLifetime lifetime, IUpdater updater)
        {
            updater.Add(lifetime, this);
        }

        public void OnFixedUpdate(float delta)
        {
            PassDelta(delta);
        }
    }

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
            var progress = _targetTime > 0f ? _currentTime / _targetTime : 1f;
            _callback?.Invoke(progress);

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