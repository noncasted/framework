#if UNITY_EDITOR
using System;

namespace Internal
{
    // Play Mode of the editor: the content directory of the project comes from the editor (its roots are the Content
    // section of ProjectSettings/Framework.json, read by Internal.Editor, which installs the factory on load).
    internal static class EditorContentDirectories
    {
        private static Func<string, EditorContentDirectory> _create;

        // Returns the factory it replaces, for a test that puts it back.
        public static Func<string, EditorContentDirectory> Install(Func<string, EditorContentDirectory> create)
        {
            var previous = _create;
            _create = create;
            return previous;
        }

        public static EditorContentDirectory Create(string name)
        {
            if (_create == null)
                throw new InvalidOperationException("[Content] No editor content directories: Internal.Editor did not install them");

            return _create(name);
        }
    }
}
#endif
