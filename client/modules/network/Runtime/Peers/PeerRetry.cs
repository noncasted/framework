using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The pause before a new try of a lost connection: it doubles with every failure up to a ceiling and starts again
    // from the first delay after a connection opens. A retry started again replaces the pending one.
    internal sealed class PeerRetry
    {
        public PeerRetry(IReadOnlyLifetime lifetime, TimeSpan firstDelay, TimeSpan maxDelay, Action onTime)
        {
            _lifetime = lifetime;
            _firstDelay = firstDelay;
            _maxDelay = maxDelay;
            _onTime = onTime;
            _delay = firstDelay;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly TimeSpan _firstDelay;
        private readonly TimeSpan _maxDelay;
        private readonly Action _onTime;

        private ILifetime _current;
        private TimeSpan _delay;

        // The connection is open: the next loss waits for the first delay again.
        public void Reset()
        {
            _delay = _firstDelay;
        }

        public void Start()
        {
            Cancel();

            var delay = _delay;
            _delay = TimeSpan.FromTicks(Math.Min(_delay.Ticks * 2, _maxDelay.Ticks));

            var current = _lifetime.Child();
            _current = current;
            RunAsync(current, delay).NoAwait();
        }

        public void Cancel()
        {
            _current?.Terminate();
            _current = null;
        }

        private async UniTaskVoid RunAsync(ILifetime current, TimeSpan delay)
        {
            try
            {
                await UniTask.Delay(delay, DelayType.Realtime, cancellationToken: current.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_current != current)
                return;

            _current = null;
            _onTime();
        }
    }
}
