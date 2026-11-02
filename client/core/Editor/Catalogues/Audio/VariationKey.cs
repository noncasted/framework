namespace Internal
{
    internal sealed class VariationKey
    {
        public VariationKey(string name, int index)
        {
            Name = name;
            Index = index;
        }

        public string Name { get; }
        public int Index { get; }
    }
}
