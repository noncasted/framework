using System.IO;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The index of a content directory on the server, fetched once at the start into the file system of the page and read
    // from there; kept in the Cache API by its URL (named by the hash of the build). A brotli index comes with
    // Content-Encoding: br and the browser decodes it; one that comes in still compressed is no json and fails here.
    internal static class WebContentIndex
    {
        public static async UniTask<ContentIndex> Load(IReadOnlyLifetime lifetime, string url, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new IOException(path));
            await WebContentFetch.File(lifetime, url, path);

            var json = System.IO.File.ReadAllText(path);
            System.IO.File.Delete(path);

            if (json.StartsWith("{") == false)
                throw new InvalidDataException($"[Content] {url} is no json: the server must send a .br file with Content-Encoding: br, and the browser decodes it only on https");

            return ContentIndex.FromJson(json);
        }
    }
}
