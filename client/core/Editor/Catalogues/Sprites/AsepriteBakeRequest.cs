using System.Collections.Generic;

namespace Internal
{
    internal sealed class AsepriteBakeRequest
    {
        public AsepriteBakeRequest(string name, AsepriteTag tag, List<string> layers)
        {
            Name = name;
            Tag = tag;
            Layers = layers;
        }

        public string Name { get; }
        public AsepriteTag Tag { get; }
        public List<string> Layers { get; }
        public int[] FrameIndices { get; private set; }

        public void SetFrameIndices(int[] frameIndices)
        {
            FrameIndices = frameIndices;
        }
    }
}
