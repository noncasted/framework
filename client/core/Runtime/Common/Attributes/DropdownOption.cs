namespace Internal
{
    public readonly struct DropdownOption
    {
        public DropdownOption(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public string Value { get; }
    }
}
