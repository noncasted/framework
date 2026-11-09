using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // Fetches the files of a content directory into the emscripten file system of the page (WebContentFetch.jslib),
    // where the native reads of the engine find them; the bytes never pass through managed code. The responses are kept
    // in the Cache API of the page (the cache "content", keyed by the URL), so the next opening reads them from there:
    // safe because every file is named by the hash of its content. Without the Cache API (plain http) every opening
    // fetches, the page logs it once. A download is aborted (AbortController) when its lifetime ends. WebGL player only.
    // A directory shipped with brotli (ContentIndex.IsBrotli) is fetched as <name>.br: the server sends Content-Encoding: br,
    // the browser decodes it, the cache keeps the real bytes; a file that comes in at another size fails the fetch.
    internal sealed class WebContentFetch : IContentFileSource
    {
        private const int Running = 0;
        private const int Fetched = 1;
        private const int Failed = 2;
        private const int Cached = 3;

        // url: the folder of the directory, StreamingAssets of the build or a CDN; suffix: ContentIndex.ShippedSuffix.
        public WebContentFetch(string url, string suffix)
        {
            _url = url;
            _suffix = suffix;
        }

        private readonly string _url;
        private readonly string _suffix;

        public string Url(ContentFile file)
        {
            return $"{_url}/{file.Name}{_suffix}";
        }

        // Removes from the cache of the page every file the index does not name: the files of the builds before.
        public void KeepOnly(ContentIndex index)
        {
            var urls = new StringBuilder();

            foreach (var file in index.Files)
                urls.Append(Url(file)).Append('\n');

            WebContentFetch_KeepOnly(urls.ToString());
        }

        public async UniTask Fetch(IReadOnlyLifetime lifetime, ContentFile file, string path)
        {
            lifetime.Token.ThrowIfCancellationRequested();

            var url = Url(file);
            var fetch = WebContentFetch_Start(url, path, file.Size, _suffix.Length > 0);
            var isOver = false;

            try
            {
                while (true)
                {
                    var status = WebContentFetch_Status(fetch);

                    if (status == Fetched || status == Cached)
                    {
                        isOver = true;
                        return;
                    }

                    if (status != Running)
                    {
                        isOver = true;
                        throw new IOException($"[Content] Fetching {url} into {path} failed with the status {status} (the reason is in the console of the page)");
                    }

                    await UniTask.Yield(lifetime.Token);
                }
            }
            finally
            {
                if (isOver == false)
                    WebContentFetch_Abort(fetch);
            }
        }

        // encoded: the URL is a compressed file the server must send with its Content-Encoding (for the message of a failure).
        [DllImport("__Internal")]
        private static extern int WebContentFetch_Start(string url, string path, double size, bool encoded);

        // Running, Fetched, Cached or Failed; a finished fetch is forgotten once its status is read.
        [DllImport("__Internal")]
        private static extern int WebContentFetch_Status(int fetch);

        [DllImport("__Internal")]
        private static extern void WebContentFetch_Abort(int fetch);

        // The URLs to keep, one per line.
        [DllImport("__Internal")]
        private static extern void WebContentFetch_KeepOnly(string urls);
    }
}
