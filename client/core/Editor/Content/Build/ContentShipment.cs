using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;

namespace Internal
{
    // What a player gets as StreamingAssets/Content: every file of every bundle under the name of the build
    // (<name>.<hash>.bundle), so a server and the Cache API keep it forever; a bundle a WebGL player fetches in gzip
    // (IsGzip, not preloaded) as <file>.gz alone, at the smallest size (unpacked by the page). Written into
    // Library/ContentShipped, made anew for every build.
    public static class ContentShipment
    {
        public const string Output = "Library/ContentShipped";

        // CompressionLevel.SmallestSize of .NET 6, missing from the netstandard the scripts compile against.
        private const CompressionLevel SmallestSize = (CompressionLevel)3;

        public static string Write(IReadOnlyList<ContentBuiltBundle> bundles, BuildTarget target, string folder = Output)
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);

            Directory.CreateDirectory(folder);

            foreach (var bundle in bundles)
            {
                var isGzip = IsGzip(bundle.Plan, target);

                foreach (var file in bundle.Files)
                {
                    var shipped = Path.Combine(folder, file.Name);

                    if (isGzip == false)
                    {
                        File.Copy(file.Path, shipped);
                        continue;
                    }

                    using var input = File.OpenRead(file.Path);
                    using var gzip = new GZipStream(File.Create(shipped + ContentCatalogPaths.GzipExtension), SmallestSize);
                    input.CopyTo(gzip);
                }
            }

            return folder;
        }

        // Only WebGL fetches StreamingAssets, and the .data file of a preloaded bundle is compressed by the build.
        public static bool IsPreloaded(ContentBundlePlan bundle, BuildTarget target)
        {
            return bundle.IsPreloaded && target == BuildTarget.WebGL;
        }

        public static bool IsGzip(ContentBundlePlan bundle, BuildTarget target)
        {
            return bundle.IsGzip && target == BuildTarget.WebGL && bundle.IsPreloaded == false;
        }
    }
}
