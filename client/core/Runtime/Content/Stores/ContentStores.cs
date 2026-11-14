using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    // The index and the store of a content directory on this platform: a WebGL player fetches the index and then the
    // ranges of the archive on demand unless the directory is preloaded into its .data file; every other player reads
    // StreamingAssets. Play Mode of the editor registers no directory at all (EditorContentDirectory).
    internal static class ContentStores
    {
        // RemoteUrl of the options: the folder of the content folders on a CDN (a WebGL player only), null for StreamingAssets
        // of the build; MaxFetches: the requests on their way to the server at once; RangeGap: ContentRanges.Gap.
        public static async UniTask<(IContentFiles, IContentStore)> Open(IReadOnlyLifetime lifetime, ContentStamp stamp, ContentOptions options)
        {
            var local = $"{Application.streamingAssetsPath}/{ContentPaths.Directory(options.Name)}";
#if UNITY_WEBGL && !UNITY_EDITOR
            WebContentFetch.Limit(options.MaxFetches);

            // A preloaded archive is in the .data file: nothing to fetch, so no index of its files to read either.
            if (stamp.IsPreloaded)
            {
                WebContentFetch.KeepOnly(options.Name);
                return (new PreloadedContentFiles(options.Name), new LocalContentStore(local));
            }

            var cache = $"{Application.temporaryCachePath}/{ContentPaths.Directory(options.Name)}";
            var folder = $"{options.RemoteUrl ?? Application.streamingAssetsPath}/{ContentPaths.Directory(options.Name)}";
            var indexUrl = $"{folder}/{stamp.ShippedIndex}";
            var index = stamp.Check(await WebContentIndex.Load(lifetime, indexUrl, $"{cache}.index.json"));
            var archive = new WebContentArchive(folder, stamp.ManifestHash);
            WebContentFetch.KeepOnly(options.Name, indexUrl, archive.Url);
            return (index, new FetchedContentStore(archive, cache, options.RangeGap, index));
#else
            lifetime.Token.ThrowIfCancellationRequested();
            await UniTask.CompletedTask;
            var index = stamp.Check(ContentIndex.FromJson(File.ReadAllText($"{local}/{ContentPaths.IndexFile(stamp.ManifestHash)}")));
            return (index, new LocalContentStore(local));
#endif
        }
    }
}
