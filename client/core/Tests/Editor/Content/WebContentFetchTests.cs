using NUnit.Framework;

namespace Internal.Tests
{
    // The URL a WebGL player fetches a file of its content directory from: the name of the index, with .br for a directory
    // shipped with brotli; the same URL keys the Cache API and KeepOnly.
    public class WebContentFetchTests
    {
        private static readonly ContentFile File = new("0a1b2c.cf", 16);

        [Test]
        public void Url_OfARawDirectory_IsTheNameOfTheFile()
        {
            var fetch = new WebContentFetch("https://host/StreamingAssets/Content/Game", string.Empty);

            Assert.That(fetch.Url(File), Is.EqualTo("https://host/StreamingAssets/Content/Game/0a1b2c.cf"));
        }

        [Test]
        public void Url_OfABrotliDirectory_EndsWithBr()
        {
            var fetch = new WebContentFetch("https://host/StreamingAssets/Content/Game", ContentPaths.BrotliExtension);

            Assert.That(fetch.Url(File), Is.EqualTo("https://host/StreamingAssets/Content/Game/0a1b2c.cf.br"));
        }
    }
}
