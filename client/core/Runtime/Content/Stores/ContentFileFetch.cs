using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // One download of a file of a content directory and the lifetimes waiting for it. The download has a lifetime of
    // its own, ended when the last waiter is gone: the source aborts it and writes nothing.
    internal sealed class ContentFileFetch
    {
        public ContentFileFetch(ContentFileFetches owner, ContentFile file)
        {
            _owner = owner;
            File = file;
        }

        private readonly ContentFileFetches _owner;
        private readonly Lifetime _download = new();
        private readonly UniTaskCompletionSource _completion = new();
        private int _waiters;

        public ContentFile File { get; }

        public void Start(IContentFileSource source, string path)
        {
            Run(source, path).Forget();
        }

        public async UniTask Wait(IReadOnlyLifetime waiter)
        {
            _waiters++;

            try
            {
                await _completion.Task.AttachExternalCancellation(waiter.Token);
            }
            finally
            {
                _waiters--;

                if (_waiters == 0 && _completion.Task.Status == UniTaskStatus.Pending)
                    Abandon();
            }
        }

        private void Abandon()
        {
            _owner.Forget(this);
            _download.Terminate();
        }

        // The top of a task nobody awaits: the outcome goes to the waiters through the completion.
        private async UniTaskVoid Run(IContentFileSource source, string path)
        {
            try
            {
                await source.Fetch(_download, File, path);
            }
            catch (OperationCanceledException) when (_download.IsTerminated)
            {
                // Abandoned by every waiter: nobody awaits the completion any more.
                return;
            }
            catch (Exception exception)
            {
                _owner.Forget(this);
                _completion.TrySetException(exception);
                _download.Terminate();
                return;
            }

            _owner.OnFetched(this);
            _completion.TrySetResult();
            _download.Terminate();
        }
    }
}
