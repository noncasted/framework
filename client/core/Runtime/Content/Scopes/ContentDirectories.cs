using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The content directories of the root scope by name: the one of AddContent first, then the ones of
    // AddContentDirectory, which register in the background. They live as long as the root. Public: the container of the
    // root scope of a game, generated in its own assembly, names the registered type.
    public sealed class ContentDirectories : IContentDirectories
    {
        private readonly Dictionary<string, UniTaskCompletionSource<IContentLoader>> _loaders = new(StringComparer.OrdinalIgnoreCase);

        // The registrations of ContentRegistration.OnFirstWait not started yet: the first Wait of the name starts one.
        private readonly Dictionary<string, Action> _deferred = new(StringComparer.OrdinalIgnoreCase);

        public IContentLoader Get(string name)
        {
            var task = Find(name).Task;

            if (task.Status != UniTaskStatus.Succeeded)
                throw new InvalidOperationException($"[Content] The content directory {name} is {task.Status}: Wait for its registration");

            return task.GetAwaiter().GetResult();
        }

        public UniTask<IContentLoader> Wait(IReadOnlyLifetime lifetime, string name)
        {
            var source = Find(name);

            if (_deferred.Remove(name, out var start) == true)
                start();

            return source.Task.AttachExternalCancellation(lifetime.Token);
        }

        internal void Add(string name, IContentLoader loader)
        {
            Begin(name).TrySetResult(loader);
        }

        // A directory whose registration has started: the source gets its loader or its failure.
        internal UniTaskCompletionSource<IContentLoader> Begin(string name)
        {
            if (_loaders.ContainsKey(name))
                throw new InvalidOperationException($"[Content] The content directory {name} is registered twice");

            var source = new UniTaskCompletionSource<IContentLoader>();
            _loaders.Add(name, source);
            return source;
        }

        // A directory whose registration starts at the first Wait of its name.
        internal UniTaskCompletionSource<IContentLoader> Defer(string name, Action<UniTaskCompletionSource<IContentLoader>> start)
        {
            var source = Begin(name);
            _deferred.Add(name, () => start(source));
            return source;
        }

        private UniTaskCompletionSource<IContentLoader> Find(string name)
        {
            if (_loaders.TryGetValue(name, out var source))
                return source;

            throw new InvalidOperationException(
                $"[Content] No content directory {name} in the root scope: it has {string.Join(", ", _loaders.Keys)} (AddContentDirectory registers one)");
        }
    }
}
