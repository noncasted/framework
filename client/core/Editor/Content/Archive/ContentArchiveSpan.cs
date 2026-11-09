namespace Internal
{
    // The bytes of one file in an archive of a content directory: its compressed blocks, which hold nothing else.
    public readonly struct ContentArchiveSpan
    {
        public ContentArchiveSpan(long offset, long length)
        {
            Offset = offset;
            Length = length;
        }

        public long Offset { get; }
        public long Length { get; }
        public long End => Offset + Length;

        public override string ToString() => $"[{Offset}, {End})";
    }
}
