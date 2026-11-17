using System;
using System.Collections.Generic;
using System.Linq;

namespace Internal
{
    // A shared content bundle of the game, written by ContentSharedSync: its name, how a player gets it (IsPreloaded,
    // IsGzip of ContentBundle) and the bundles it serves. An asset several bundles need directly goes into the first shared
    // bundle (in the order of IContentSharedBundles.Bundles) that serves one of them or is one of them; one that lists
    // none serves every bundle (the last one, for the rest).
    public sealed class ContentSharedBundle
    {
        public ContentSharedBundle(string name, bool isPreloaded, bool isGzip, params string[] serves)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("[ContentSharedSync] A shared content bundle without a name", nameof(name));

            Name = name;
            IsPreloaded = isPreloaded;
            IsGzip = isGzip;
            Serves = serves;
        }

        public string Name { get; }
        public bool IsPreloaded { get; }
        public bool IsGzip { get; }

        // The names of the bundles whose shared assets it takes; empty: every bundle.
        public IReadOnlyList<string> Serves { get; }

        // Takes an asset these bundles need.
        public bool Takes(IReadOnlyList<string> bundles)
        {
            return Serves.Count == 0 || bundles.Contains(Name) || bundles.Any(bundle => Serves.Contains(bundle));
        }
    }
}
