using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The bundles the root scope registered: the main one of AddContent (the catalogues load from it) and the ones of
    // AddContentBundle, each loaded whole and held while the root lives, each with its own loader (its roots: RootAssets).
    // Any loader loads any reference: the catalog knows the bundle of every lazy target.
    public interface IContentBundles
    {
        // The loader of the bundle of this name; an unknown name throws, and so does a bundle still registering (Wait for it).
        IContentLoader Get(string name);

        // The loader once the bundle is registered: AddContentBundle registers in the background while the scopes after
        // the root are built. Its failed registration throws here; an unknown name throws at once.
        UniTask<IContentLoader> Wait(IReadOnlyLifetime lifetime, string name);
    }

    // The registry of the root scope. Public: the container of the root scope of a game, generated in its own assembly,
    // names the registered type.
    public sealed class ContentBundles : IContentBundles
    {
        private readonly Dictionary<string, UniTaskCompletionSource<IContentLoader>> _loaders = new(StringComparer.OrdinalIgnoreCase);

        // The registrations of ContentRegistration.OnFirstWait not started yet: the first Wait of the name starts one.
        private readonly Dictionary<string, Action> _deferred = new(StringComparer.OrdinalIgnoreCase);

        public IContentLoader Get(string name)
        {
            var task = Find(name).Task;

            if (task.Status != UniTaskStatus.Succeeded)
                throw new InvalidOperationException($"[Content] The content bundle {name} is {task.Status}: Wait for its registration");

            return task.GetAwaiter().GetResult();
        }

        public UniTask<IContentLoader> Wait(IReadOnlyLifetime lifetime, string name)
        {
            var source = Find(name);

            if (_deferred.Remove(name, out var start))
                start();

            return source.Task.AttachExternalCancellation(lifetime.Token);
        }

        internal void Add(string name, IContentLoader loader)
        {
            Begin(name).TrySetResult(loader);
        }

        // A bundle whose registration has started: the source gets its loader or its failure.
        internal UniTaskCompletionSource<IContentLoader> Begin(string name)
        {
            if (_loaders.ContainsKey(name))
                throw new InvalidOperationException($"[Content] The content bundle {name} is registered twice");

            var source = new UniTaskCompletionSource<IContentLoader>();
            _loaders.Add(name, source);
            return source;
        }

        // A bundle whose registration starts at the first Wait of its name.
        internal void Defer(string name, Action<UniTaskCompletionSource<IContentLoader>> start)
        {
            var source = Begin(name);
            _deferred.Add(name, () => start(source));
        }

        private UniTaskCompletionSource<IContentLoader> Find(string name)
        {
            if (_loaders.TryGetValue(name, out var source))
                return source;

            throw new InvalidOperationException(
                $"[Content] No content bundle {name} in the root scope: it has {string.Join(", ", _loaders.Keys)} (AddContentBundle registers one)");
        }
    }
}
