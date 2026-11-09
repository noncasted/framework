using System;
using NUnit.Framework;

namespace Internal.Tests
{
    // The requests a fetch of the archive makes: neighbours at most the gap apart go together, with the files between.
    public class ContentRangesTests
    {
        private static readonly ContentFile Header = new(ContentIndex.HeaderName, 0, 64);
        private static readonly ContentFile A = new("a.cf", 64, 100);
        private static readonly ContentFile B = new("b.cf", 176, 50);
        private static readonly ContentFile C = new("c.cf", 240, 10);
        private static readonly ContentFile D = new("d.cf", 1000, 10);

        private static readonly ContentFile[] Files = { Header, A, B, C, D };

        [Test]
        public void Neighbours_WithinTheGap_OneRangeWithTheFilesBetween()
        {
            var ranges = new ContentRanges(Files, 100).Merge(new[] { Header, A, C });

            Assert.AreEqual(1, ranges.Count);
            Assert.AreEqual(0, ranges[0].Offset);
            Assert.AreEqual(C.End, ranges[0].End);
            CollectionAssert.AreEqual(new[] { Header, A, B, C }, ranges[0].Files);
        }

        [Test]
        public void Files_FurtherThanTheGap_RangesApart()
        {
            var ranges = new ContentRanges(Files, 16).Merge(new[] { A, D });

            Assert.AreEqual(2, ranges.Count);
            CollectionAssert.AreEqual(new[] { A }, ranges[0].Files);
            CollectionAssert.AreEqual(new[] { D }, ranges[1].Files);
            Assert.AreEqual(D.Size, ranges[1].Length);
        }

        [Test]
        public void ZeroGap_MergesOnlyTouchingFiles()
        {
            Assert.AreEqual(2, new ContentRanges(Files, 0).Merge(new[] { A, B }).Count, "12 bytes of padding between");
            Assert.AreEqual(1, new ContentRanges(Files, 0).Merge(new[] { Header, A }).Count);
        }

        [Test]
        public void NothingNeeded_NoRange()
        {
            Assert.IsEmpty(new ContentRanges(Files, 16).Merge(Array.Empty<ContentFile>()));
        }

        [Test]
        public void FilesOutOfOrder_Throw()
        {
            Assert.Throws<ArgumentException>(() => new ContentRanges(new[] { A, Header }, 16));
            Assert.Throws<ArgumentException>(() => new ContentRanges(Files, 1000).Merge(new[] { B, A }));
        }
    }
}
