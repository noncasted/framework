namespace Internal
{
    public sealed class AsepriteTag
    {
        public AsepriteTag(string name, int from, int to)
        {
            Name = name;
            From = from;
            To = to;
        }

        public string Name { get; }
        public int From { get; }
        public int To { get; }
        public int FrameCount => To - From + 1;
    }
}
