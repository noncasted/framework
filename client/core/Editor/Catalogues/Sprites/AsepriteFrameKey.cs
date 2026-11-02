using System.Collections.Generic;
using System.Text;

namespace Internal
{
    internal sealed class AsepriteFrameKey
    {
        public AsepriteFrameKey(int frame, List<string> layers)
        {
            Frame = frame;
            Layers = layers;

            var builder = new StringBuilder();
            builder.Append(frame);

            foreach (var layer in layers)
                builder.Append('|').Append(layer);

            Id = builder.ToString();
        }

        public int Frame { get; }
        public List<string> Layers { get; }
        public string Id { get; }
    }
}
