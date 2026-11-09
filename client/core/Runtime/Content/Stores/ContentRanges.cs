using System;
using System.Collections.Generic;

namespace Internal
{
    // Turns the files a fetch needs into requests for the archive: neighbours whose spans are at most Gap bytes apart
    // go into one request, which then also brings the files between them. A level needs thousands of small files laid
    // out close together in the order of first use, so this takes them in tens of requests instead of thousands.
    internal sealed class ContentRanges
    {
        // files: the header and every file of the archive, in the order of their offsets, apart.
        public ContentRanges(IReadOnlyList<ContentFile> files, long gap)
        {
            if (gap < 0)
                throw new ArgumentOutOfRangeException(nameof(gap), gap, "[Content] A negative gap between the ranges of the archive");

            for (var i = 1; i < files.Count; i++)
            {
                if (files[i].Offset < files[i - 1].End)
                    throw new ArgumentException($"[Content] {files[i]} lies before the end of {files[i - 1]}", nameof(files));
            }

            _files = files;
            Gap = gap;
        }

        private readonly IReadOnlyList<ContentFile> _files;

        public long Gap { get; }

        // needed: files of the archive in the order of their offsets, each once.
        public IReadOnlyList<ContentRange> Merge(IReadOnlyList<ContentFile> needed)
        {
            var ranges = new List<ContentRange>();
            var at = 0;

            while (at < needed.Count)
            {
                var offset = needed[at].Offset;
                var end = needed[at].End;
                at++;

                while (at < needed.Count && needed[at].Offset - end <= Gap)
                {
                    if (needed[at].Offset < end)
                        throw new ArgumentException($"[Content] {needed[at]} is not after the files before it", nameof(needed));

                    end = needed[at].End;
                    at++;
                }

                ranges.Add(new ContentRange(offset, end, Inside(offset, end)));
            }

            return ranges;
        }

        private IReadOnlyList<ContentFile> Inside(long offset, long end)
        {
            var inside = new List<ContentFile>();

            for (var i = First(offset); i < _files.Count && _files[i].Offset < end; i++)
            {
                if (_files[i].End <= end)
                    inside.Add(_files[i]);
            }

            return inside;
        }

        // The first file that starts at the offset or after it.
        private int First(long offset)
        {
            var low = 0;
            var high = _files.Count;

            while (low < high)
            {
                var middle = (low + high) / 2;

                if (_files[middle].Offset < offset)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }
    }
}
