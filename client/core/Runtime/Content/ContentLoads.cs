using System.Collections.Generic;

namespace Internal
{
    // The running loads and the progress of every reference, shared by the loaders of the bundles of one root scope: a
    // reference loaded through two loaders is one load, so its Target is one object. Main thread only.
    internal sealed class ContentLoads
    {
        public Dictionary<ContentReference, object> Objects { get; } = new();
        public Dictionary<string, ViewableProperty<ContentProgress>> Progress { get; } = new();
    }
}
