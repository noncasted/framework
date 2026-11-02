namespace Internal
{
    public sealed class AsepriteLayer
    {
        public AsepriteLayer(int index, string name, bool visible, float opacity)
        {
            Index = index;
            Name = name;
            Visible = visible;
            Opacity = opacity;
        }

        public int Index { get; }
        public string Name { get; }
        public bool Visible { get; }
        public float Opacity { get; }
    }
}
