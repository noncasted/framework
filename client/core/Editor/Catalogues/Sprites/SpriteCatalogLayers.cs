using System.Collections.Generic;

namespace Internal
{
    internal static class SpriteCatalogLayers
    {
        internal static void Set(List<string> layers, string layer, bool enabled)
        {
            layers.Remove(layer);

            if (enabled)
                layers.Add(layer);
        }
    }
}
