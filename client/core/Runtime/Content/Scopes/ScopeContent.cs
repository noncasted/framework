using System;

namespace Internal
{
    // The content loader a scope loads its scenes and content with: the one of the parent scope, or in the root the one
    // AddContent registers. Taken from the parent on the first use, so a scope that loads no content needs none.
    public sealed class ScopeContent
    {
        internal ScopeContent(ILoadedScope parent)
        {
            _parent = parent;
        }

        private readonly ILoadedScope _parent;
        private IContentLoader _loader;

        public IContentLoader Loader
        {
            get
            {
                if (_loader != null)
                    return _loader;

                if (_parent == null)
                    throw new InvalidOperationException("[Content] The root scope has no content loader: call AddContent in its Construct first");

                _loader = _parent.Container.Resolve<IContentLoader>();
                return _loader;
            }
        }

        internal void Use(IContentLoader loader)
        {
            if (_parent != null)
                throw new InvalidOperationException("[Content] AddContent belongs to the root scope: a child takes the loader of its parent");

            if (_loader != null)
                throw new InvalidOperationException("[Content] AddContent is called twice in one scope");

            _loader = loader;
        }
    }
}
