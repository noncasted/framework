using NUnit.Framework;

namespace Internal.Tests
{
    // The URLs a WebGL player fetches its content directory from: the archive and the index named by the hash of the
    // build, the index with .br when it is shipped with brotli; the same URLs key the Cache API and KeepOnly.
    public class WebContentArchiveTests
    {
        private const string Folder = "https://host/StreamingAssets/Content/Game";
        private const string Hash = "0a1b2c";

        [Test]
        public void Url_IsTheArchiveOfTheBuild()
        {
            Assert.That(new WebContentArchive(Folder, Hash).Url, Is.EqualTo($"{Folder}/0a1b2c.archive"));
        }

        [Test]
        public void ShippedIndex_OfARawIndex_IsItsName()
        {
            Assert.That(new ContentStamp(Hash, false, false).ShippedIndex, Is.EqualTo("0a1b2c.index.json"));
        }

        [Test]
        public void ShippedIndex_OfABrotliIndex_EndsWithBr()
        {
            Assert.That(new ContentStamp(Hash, false, true).ShippedIndex, Is.EqualTo("0a1b2c.index.json.br"));
        }

        [Test]
        public void Stamp_PreloadedWithBrotli_Throws()
        {
            Assert.Throws<System.IO.InvalidDataException>(() => new ContentStamp(Hash, true, true));
        }

        [Test]
        public void Stamp_Json_GivesTheSameStampBack()
        {
            var stamp = ContentStamp.FromJson(new ContentStamp(Hash, true, false).ToJson());

            Assert.That(stamp.ManifestHash, Is.EqualTo(Hash));
            Assert.That(stamp.IsPreloaded, Is.True);
            Assert.That(stamp.IsIndexBrotli, Is.False);
        }
    }
}
