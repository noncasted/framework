using System;
using System.IO;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;

namespace Internal
{
    // The fetches of a WebGL player into the emscripten file system of the page (WebContentFetch.jslib), where the native
    // reads of the engine find the bytes; they never pass through managed code. The responses are kept in the Cache API of
    // the page (the cache "content"), so the next opening reads them from there: safe because the index and the archive
    // are named by the hash of the build. Without the Cache API (plain http) every opening fetches, the page logs it once.
    // A fetch is aborted (AbortController) when its lifetime ends. At most Limit requests go to the server at once (the
    // page queues the others in the order they started and holds a slot until the body is read); reads from the cache of
    // the page take no slot. WebGL player only.
    internal static class WebContentFetch
    {
        private const int Running = 0;
        private const int Fetched = 1;
        private const int Failed = 2;
        private const int Cached = 3;

        // The requests of the page that go to the server at once (ContentOptions.MaxFetches), for every directory.
        public static void Limit(int fetches)
        {
            if (fetches < 1)
                throw new ArgumentOutOfRangeException(nameof(fetches), fetches, "[Content] At least one fetch must run");

            WebContentFetch_Limit(fetches);
        }

        // A whole file of unknown size into path (the index), kept in the cache by its URL.
        public static UniTask File(IReadOnlyLifetime lifetime, string url, string path)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            return Await(lifetime, WebContentFetch_Start(url, path), $"{url} into {path}");
        }

        // The bytes [offset, offset + length) of url written at the same offset of the file at path (it exists at its
        // full length). files: "<offset>:<length>" of each file wholly inside, a line each: the cache keeps every one apart.
        public static UniTask Range(IReadOnlyLifetime lifetime, string url, string path, long offset, long length, string files)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            return Await(lifetime, WebContentFetch_StartRange(url, path, offset, length, files), $"{url} [{offset}, {offset + length}) into {path}");
        }

        // The writes of the fetches after this keep the times the file at path has now.
        public static void Pin(string path)
        {
            WebContentFetch_Pin(path);
        }

        // Removes from the cache of the page every entry of the directory (ContentPaths.Directory of its name, wherever
        // it was fetched from) of another URL than these: the files of its builds before. The other directories keep theirs.
        public static void KeepOnly(string name, params string[] urls)
        {
            WebContentFetch_KeepOnly(ContentPaths.Directory(name), string.Join("\n", urls));
        }

        private static async UniTask Await(IReadOnlyLifetime lifetime, int fetch, string what)
        {
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
                        throw new IOException($"[Content] Fetching {what} failed with the status {status} (the reason is in the console of the page)");
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

        [DllImport("__Internal")]
        private static extern int WebContentFetch_Start(string url, string path);

        [DllImport("__Internal")]
        private static extern int WebContentFetch_StartRange(string url, string path, double offset, double length, string files);

        // Running, Fetched, Cached or Failed; a finished fetch is forgotten once its status is read.
        [DllImport("__Internal")]
        private static extern int WebContentFetch_Status(int fetch);

        [DllImport("__Internal")]
        private static extern void WebContentFetch_Abort(int fetch);

        [DllImport("__Internal")]
        private static extern void WebContentFetch_Limit(int fetches);

        [DllImport("__Internal")]
        private static extern void WebContentFetch_Pin(string path);

        // directory: Content/<name>, only the entries whose URL holds /Content/<name>/ are looked at. The URLs to keep, one
        // per line; an entry is kept when its URL without the query is one of them.
        [DllImport("__Internal")]
        private static extern void WebContentFetch_KeepOnly(string directory, string urls);
    }
}
