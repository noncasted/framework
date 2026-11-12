using System;
using System.Collections.Generic;

namespace Internal
{
    // The content directories of the root scope by name: the one of AddContent first, then the ones of
    // AddContentDirectory. They live as long as the root.
    internal sealed class ContentDirectories : IContentDirectories
    {
        private readonly Dictionary<string, IContentLoader> _loaders = new(StringComparer.OrdinalIgnoreCase);

        public IContentLoader Get(string name)
        {
            if (_loaders.TryGetValue(name, out var loader))
                return loader;

            throw new InvalidOperationException(
                $"[Content] No content directory {name} in the root scope: it has {string.Join(", ", _loaders.Keys)} (AddContentDirectory registers one)");
        }

        public void Add(string name, IContentLoader loader)
        {
            ThrowIfRegistered(name);
            _loaders.Add(name, loader);
        }

        public void ThrowIfRegistered(string name)
        {
            if (_loaders.ContainsKey(name))
                throw new InvalidOperationException($"[Content] The content directory {name} is registered twice");
        }
    }
}
