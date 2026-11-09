using System.Text;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The archive of a content directory on the server (<folder>/<hash>.archive), fetched in Range requests: the server
    // answers 206 with the bytes as they are (no Content-Encoding: the blocks are LZ4 already); any other answer fails the
    // fetch with its reason. The Cache API keeps every file of a range apart under <url>?offset=<offset> (a fragment
    // would not do: the Cache API matches URLs without it), so a later opening reads a range from the cache only when it
    // holds every file of the range. WebGL player only.
    internal sealed class WebContentArchive : IContentRangeSource
    {
        // folder: the folder of the directory, StreamingAssets of the build or a CDN.
        public WebContentArchive(string folder, string manifestHash)
        {
            Url = $"{folder}/{ContentPaths.ShippedArchive(manifestHash)}";
        }

        public string Url { get; }

        public UniTask Fetch(IReadOnlyLifetime lifetime, ContentRange range, string path)
        {
            var files = new StringBuilder();

            foreach (var file in range.Files)
                files.Append(file.Offset).Append(':').Append(file.Size).Append('\n');

            return WebContentFetch.Range(lifetime, Url, path, range.Offset, range.Length, files.ToString());
        }

        public void Pin(string path)
        {
            WebContentFetch.Pin(path);
        }
    }
}
