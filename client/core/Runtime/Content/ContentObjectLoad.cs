using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Internal
{
    // The load of one content reference and the lifetimes holding it: one load of the source for all of them, held (its
    // bundle with its dependencies) on a lifetime of its own that ends after the last one. The last holder gone before
    // the load is over ends it: the fetch is aborted, a load of the engine already started is let go when it is over.
    // Main thread only.
    internal sealed class ContentObjectLoad<T> where T : Object
    {
        public ContentObjectLoad(ContentReference<T> reference, IContentSource source, IProgress<long> progress, Action<ContentReference> onOver)
        {
            _reference = reference;
            _source = source;
            _progress = progress;
            _onOver = onOver;
        }

        private readonly ContentReference<T> _reference;
        private readonly IContentSource _source;
        private readonly IProgress<long> _progress;
        private readonly Action<ContentReference> _onOver;
        private readonly UniTaskCompletionSource<T> _completion = new();
        private readonly Lifetime _hold = new();
        private readonly List<Hold> _holds = new();
        private ContentLoadPhase _phase = ContentLoadPhase.Loading;

        public bool IsLoaded => _phase == ContentLoadPhase.Loaded;

        // Starts the load of the reference for its first holder.
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

        // The bundle is real: loaded now, without a frame.
        public T LoadNow(IReadOnlyLifetime lifetime)
        {
            if (_holds.Count > 0)
                throw new InvalidOperationException($"[Content] {_reference} is loading already");

            var load = AssetLoads.Begin($"Content: {_reference.Guid}");
            T target;

            try
            {
                target = _source.LoadNow(_hold, _reference);
            }
            catch (Exception exception)
            {
                load.Fail(exception);
                End();
                throw;
            }

            load.Finish($"Content: {target.name}");
            Loaded(target);
            AddHold(lifetime);
            return target;
        }

        public void AddHold(IReadOnlyLifetime lifetime)
        {
            var hold = new Hold(this, lifetime);
            _holds.Add(hold);
            lifetime.Listen(hold.OnEnded);
        }

        // The top of a task nobody awaits: the outcome goes to the holders through the completion.
        private async UniTaskVoid Run()
        {
            var load = AssetLoads.Begin($"Content: {_reference.Guid}");
            using var span = GameProfiler.Concurrent($"Content: {typeof(T).Name}");
            T target;

            try
            {
                target = await _source.Load(_hold, _reference, _progress);
            }
            catch (Exception exception)
            {
                load.Fail(exception);
                End();

                if (exception is OperationCanceledException)
                    _completion.TrySetCanceled();
                else
                    _completion.TrySetException(exception);

                return;
            }

            load.Finish($"Content: {target.name}");

            // Every holder ended during the load: End has let the bundle go.
            if (_phase == ContentLoadPhase.Over)
            {
                _completion.TrySetCanceled();
                return;
            }

            Loaded(target);
            _completion.TrySetResult(target);
        }

        private void Loaded(T target)
        {
            _phase = ContentLoadPhase.Loaded;
            _reference.Fill(target);
        }

        private void OnHoldEnded(Hold hold)
        {
            _holds.Remove(hold);

            if (_holds.Count == 0)
                End();
        }

        private void End()
        {
            if (_phase == ContentLoadPhase.Over)
                return;

            _phase = ContentLoadPhase.Over;

            foreach (var hold in _holds)
                hold.Lifetime.RemoveListener(hold.OnEnded);

            _holds.Clear();
            _reference.Clear();
            _hold.Terminate();
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
