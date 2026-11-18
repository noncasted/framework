using System.Collections.Generic;

namespace Internal
{
    // An asset several bundles reach directly and none holds (the engine would copy it into each), as data for a tool
    // that fixes it (ContentSharedSync): its path, the names of the bundles that need it, in their order, and the error.
    public sealed class ContentDuplicate
    {
        public ContentDuplicate(string path, IReadOnlyList<string> bundles, string error)
        {
            Path = path;
            Bundles = bundles;
            Error = error;
        }

        public string Path { get; }
        public IReadOnlyList<string> Bundles { get; }
        public string Error { get; }
    }
}
