using System.Collections.Generic;

namespace Internal
{
    // Where the files of an archive of a content directory lie in it (ContentArchiveReader): the engine mounts the
    // archive reading only its header (the table of contents included) and reads the blocks of a file when it opens the
    // file, so a file whose span holds zeros is fine until something loads it.
    public sealed class ContentArchiveLayout
    {
        public ContentArchiveLayout(long length, long headerLength, IReadOnlyDictionary<string, ContentArchiveSpan> files)
        {
            Length = length;
            HeaderLength = headerLength;
            Files = files;
        }

        // The bytes of the archive.
        public long Length { get; }

        // [0, HeaderLength): the header and the table of contents, what the mount reads.
        public long HeaderLength { get; }

        // By the name of the file in the archive.
        public IReadOnlyDictionary<string, ContentArchiveSpan> Files { get; }
    }
}
