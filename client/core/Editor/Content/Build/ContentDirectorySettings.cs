using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Internal
{
    // The content directory of the project, the "Content" section of ProjectSettings/Framework.json: every player build
    // builds it (ContentBuild). Name is the folder StreamingAssets/Content/<Name> and the name of the index; Roots are
    // the paths of the root assets (the ContentRoot of the game); a preloaded directory goes into the .data file of a
    // WebGL build, the others are fetched by the player.
    public sealed class ContentDirectorySettings
    {
        [JsonConstructor]
        public ContentDirectorySettings(string name, IReadOnlyList<string> roots, bool isPreloaded)
        {
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"[FrameworkSettings] {FrameworkSettings.SettingsPath}: the Content section has no Name.");

            if (roots == null || roots.Count == 0)
                throw new InvalidOperationException($"[FrameworkSettings] {FrameworkSettings.SettingsPath}: the content directory {name} has no Roots.");

            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root))
                    throw new InvalidOperationException($"[FrameworkSettings] {FrameworkSettings.SettingsPath}: the content directory {name} has an empty root.");
            }

            Name = name;
            Roots = roots;
            IsPreloaded = isPreloaded;
        }

        public string Name { get; }
        public IReadOnlyList<string> Roots { get; }
        public bool IsPreloaded { get; }
    }
}
