using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // One fetch of a file for every load that waits for it: the work runs once, on a lifetime of its own that ends (the
    // request aborted) when the last waiter leaves before it is over. The waiters wait on a completion source: a UniTask
    // takes one awaiter at a time, Preserve included. Main thread only.
    internal sealed class ContentFileFetch
    {
        public ContentFileFetch(Func<IReadOnlyLifetime, UniTask> work, Action onOver)
        {
            _onOver = onOver;
            Run(work).NoAwait();
        }

        private readonly Action _onOver;
        private readonly Lifetime _lifetime = new();
        private readonly UniTaskCompletionSource _completion = new();
        private int _waiters;
        private bool _isOver;

        public async UniTask Wait(IReadOnlyLifetime lifetime)
        {
            lifetime.Token.ThrowIfCancellationRequested();

            _waiters++;
            Action ended = Leave;
            lifetime.Listen(ended);

            try
            {
                await _completion.Task.AttachExternalCancellation(lifetime.Token);
            }
            finally
            {
                if (lifetime.IsTerminated == false)
                {
                    lifetime.RemoveListener(ended);
                    _waiters--;
                }
            }
        }

        private void Leave()
        {
            _waiters--;

            if (_waiters > 0 || _isOver)
                return;

            Over();
            _lifetime.Terminate();
        }

        // The top of a task nobody awaits: the outcome goes to the waiters through the completion.
        private async UniTaskVoid Run(Func<IReadOnlyLifetime, UniTask> work)
        {
            try
            {
                // The work starts in the next frame: the first waiter is counted before it can end.
                await UniTask.Yield(_lifetime.Token);
                await work(_lifetime);
                _completion.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                _completion.TrySetCanceled();
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
            finally
            {
                if (_isOver == false)
                    Over();
            }
        }

        private void Over()
        {
            _isOver = true;
            _onOver();
        }
    }
}
