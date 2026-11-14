using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Internal
{
    // The load of one content reference and the lifetimes holding it. The engine loads a Loadable once and its first
    // Release frees it, so the lifetimes are counted here: one LoadAsync for all of them, one Release after the last.
    // The last holder gone during the fetch aborts the fetch; during the engine load, the load goes on and is released
    // when it ends; a holder that comes meanwhile keeps it. Main thread only.
    internal sealed class ContentObjectLoad<T> where T : Object
    {
        public ContentObjectLoad(ContentReference<T> reference, IContentDirectory directory, IProgress<long> progress, Action<ContentReference> onOver)
        {
            _reference = reference;
            _directory = directory;
            _progress = progress;
            _onOver = onOver;
        }

        private readonly ContentReference<T> _reference;
        private readonly IContentDirectory _directory;
        private readonly IProgress<long> _progress;
        private readonly Action<ContentReference> _onOver;
        private readonly UniTaskCompletionSource<T> _completion = new();
        private readonly Lifetime _fetch = new();
        private readonly List<Hold> _holds = new();
        private ContentLoadPhase _phase = ContentLoadPhase.Fetching;

        public bool IsLoaded => _phase == ContentLoadPhase.Loaded;

        // Starts the fetch and the load of the reference for its first holder.
        public UniTask<T> Start(IReadOnlyLifetime lifetime)
        {
            var wait = Add(lifetime);
            Run().NoAwait();
            return wait;
        }

        public UniTask<T> Add(IReadOnlyLifetime lifetime)
        {
            AddHold(lifetime);
            return _completion.Task.AttachExternalCancellation(lifetime.Token);
        }

        // The files are real: the engine loads it now, without a frame.
        public T LoadNow(IReadOnlyLifetime lifetime)
        {
            if (_phase != ContentLoadPhase.Fetching || _holds.Count > 0)
                throw new InvalidOperationException($"[Content] {_reference} is loading already");

            var load = AssetLoads.Begin($"Content: {_reference.Guid}");
            _phase = ContentLoadPhase.Loading;
            T target;

            using (GameProfiler.Scope($"Content: load {typeof(T).Name}"))
                target = _reference.Loadable.Load();

            if (target == null)
            {
                var exception = new InvalidOperationException($"[Content] The engine did not load {typeof(T).Name} {_reference} ({_reference.Loadable.Status})");
                load.Fail(exception);
                _reference.Loadable.Release();
                End();
                throw exception;
            }

            load.Finish($"Content: {target.name}");
            _phase = ContentLoadPhase.Loaded;
            _completion.TrySetResult(target);
            AddHold(lifetime);
            return target;
        }

        // The top of a task nobody awaits: the outcome goes to the holders through the completion.
        private async UniTaskVoid Run()
        {
            var load = AssetLoads.Begin($"Content: {_reference.Guid}");
            using var span = GameProfiler.Concurrent($"Content: {typeof(T).Name}");
            T target;

            try
            {
                await _directory.FetchObject(_fetch, _reference.Guid, _reference.LocalId, _progress);
                _fetch.Token.ThrowIfCancellationRequested();

                _phase = ContentLoadPhase.Loading;
                target = await _reference.Loadable.LoadAsync();

                if (target == null)
                    throw new InvalidOperationException($"[Content] The engine did not load {typeof(T).Name} {_reference} ({_reference.Loadable.Status})");
            }
            catch (Exception exception)
            {
                load.Fail(exception);

                if (_phase == ContentLoadPhase.Loading)
                    _reference.Loadable.Release();

                End();
                _completion.TrySetException(exception);
                return;
            }

            load.Finish($"Content: {target.name}");

            // Every holder ended during the engine load.
            if (_holds.Count == 0)
            {
                _reference.Loadable.Release();
                End();
                _completion.TrySetCanceled();
                return;
            }

            _phase = ContentLoadPhase.Loaded;
            _completion.TrySetResult(target);
        }

        public void AddHold(IReadOnlyLifetime lifetime)
        {
            var hold = new Hold(this, lifetime);
            _holds.Add(hold);
            lifetime.Listen(hold.OnEnded);
        }

        private void OnHoldEnded(Hold hold)
        {
            _holds.Remove(hold);

            if (_holds.Count > 0)
                return;

            switch (_phase)
            {
                case ContentLoadPhase.Fetching:
                    // A new load of the reference starts over; this one ends with the cancelled fetch.
                    End();
                    break;
                case ContentLoadPhase.Loaded:
                    _reference.Loadable.Release();
                    End();
                    break;
            }
        }

        private void End()
        {
            if (_phase == ContentLoadPhase.Over)
                return;

            _phase = ContentLoadPhase.Over;

            foreach (var hold in _holds)
                hold.Lifetime.RemoveListener(hold.OnEnded);

            _holds.Clear();
            _fetch.Terminate();
            _onOver(_reference);
        }

        private sealed class Hold
        {
            public Hold(ContentObjectLoad<T> owner, IReadOnlyLifetime lifetime)
            {
                _owner = owner;
                Lifetime = lifetime;
                OnEnded = () => _owner.OnHoldEnded(this);
            }

            private readonly ContentObjectLoad<T> _owner;

            public IReadOnlyLifetime Lifetime { get; }
            public Action OnEnded { get; }
        }
    }
}
