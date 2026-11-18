#if UNITY_EDITOR
using System;

namespace Internal
{
    // Play Mode of the editor: the bundles of the project come from the editor (the roots of the ContentBundle asset of the
    // name, read by Internal.Editor, which installs the factory on load).
    internal static class EditorContentSourceFactory
    {
        private static Func<string, IContentSource> _create;

        // Returns the factory it replaces, for a test that puts it back.
        public static Func<string, IContentSource> Install(Func<string, IContentSource> create)
        {
            var previous = _create;
            _create = create;
            return previous;
        }

        public static IContentSource Create(string name)
        {
            if (_create == null)
                throw new InvalidOperationException("[Content] No editor content bundles: Internal.Editor did not install them");

            return _create(name);
        }
    }
}
#endif
