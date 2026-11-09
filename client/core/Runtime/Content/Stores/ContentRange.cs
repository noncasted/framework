using System.Collections.Generic;

namespace Internal
{
    // One request for the archive of a fetched content directory: the bytes [Offset, End) and the files wholly inside
    // them, the ones a load asked for and the ones between them. Every one of the files is real once the bytes are written.
    internal sealed class ContentRange
    {
        public ContentRange(long offset, long end, IReadOnlyList<ContentFile> files)
        {
            Offset = offset;
            End = end;
            Files = files;
        }

        public long Offset { get; }
        public long End { get; }
        public long Length => End - Offset;
        public IReadOnlyList<ContentFile> Files { get; }

        public override string ToString() => $"[{Offset}, {End}) of {Files.Count} files";
    }
}
