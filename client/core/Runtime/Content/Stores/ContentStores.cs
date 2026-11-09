using UnityEngine;

namespace Internal
{
    // The store of a content directory on this platform: a WebGL player fetches the files on demand unless the directory
    // is preloaded into its .data file; every other player reads StreamingAssets. Play Mode of the editor registers no
    // directory at all (EditorContentDirectory).
    internal static class ContentStores
    {
        // remoteUrl: the folder of the content folders on a CDN (a WebGL player only), null for StreamingAssets of the build.
        public static IContentStore Create(ContentIndex index, string remoteUrl)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (index.IsPreloaded)
                return new LocalContentStore(StreamingFolder(index));

            var source = new WebContentFetch($"{remoteUrl ?? Application.streamingAssetsPath}/{ContentPaths.Directory(index.Name)}", index.ShippedSuffix);
            source.KeepOnly(index);
            return new FetchedContentStore(source, $"{Application.temporaryCachePath}/{ContentPaths.Directory(index.Name)}");
#else
            return new LocalContentStore(StreamingFolder(index));
#endif
        }

        private static string StreamingFolder(ContentIndex index)
        {
            return $"{Application.streamingAssetsPath}/{ContentPaths.Directory(index.Name)}";
        }
    }
}
