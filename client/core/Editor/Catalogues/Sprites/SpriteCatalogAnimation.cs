using System.Collections.Generic;

namespace Internal
{
    // Animation from an aseprite tag: the name comes from the tag, only the enabled layers are configurable.
    public sealed class SpriteCatalogAnimation
    {
        private readonly List<string> _layers;

        public SpriteCatalogAnimation(string tag, List<string> layers)
        {
            Tag = tag;
            _layers = layers;
        }

        public string Tag { get; }
        public IReadOnlyList<string> Layers => _layers;

        public void SetLayer(string layer, bool enabled)
        {
            SpriteCatalogLayers.Set(_layers, layer, enabled);
        }
    }
}
