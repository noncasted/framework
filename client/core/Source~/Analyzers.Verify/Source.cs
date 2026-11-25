namespace Framework.Analyzers.Verify
{
    // A file of a case: its path decides the code kind (tests, generated) and the name checks.
    public readonly struct Source
    {
        public Source(string path, string text)
        {
            Path = path;
            Text = text;
        }

        public string Path { get; }
        public string Text { get; }
    }
}
