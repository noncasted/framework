using System.Collections.Generic;

namespace Internal
{
    // Additional animation on top of a source tag with its own name and layer set.
    public sealed class SpriteCatalogCustomAnimation
    {
        private readonly List<string> _layers;

        public SpriteCatalogCustomAnimation(string name, string source, List<string> layers)
        {
            Name = name;
            Source = source;
            _layers = layers;
        }

        public string Name { get; private set; }
        public string Source { get; private set; }
        public IReadOnlyList<string> Layers => _layers;

        public void Rename(string name)
        {
            Name = name;
        }

        public void SetSource(string source)
        {
            Source = source;
        }

        public void SetLayer(string layer, bool enabled)
        {
            SpriteCatalogLayers.Set(_layers, layer, enabled);
        }
    }
}
