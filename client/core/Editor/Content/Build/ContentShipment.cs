using System.IO;
using UnityEditor.Build;

namespace Internal
{
    // What a player gets as StreamingAssets/Content/<name>. A player that reads the folder in place (standalone, a
    // directory preloaded into the .data file of WebGL) gets the folder of the archive as the registration mounts it
    // (content0.archive, BuildManifestHash.txt) with <hash>.index.json in it. A WebGL player that fetches the directory
    // gets <hash>.archive and its index (<hash>.index.json, .br with brotli) in Library/ContentShipped/<name>: names of
    // this build only, so a server and the Cache API keep them forever; it writes the files of the registration itself.
    public static class ContentShipment
    {
        public const string Output = "Library/ContentShipped";

        // archive: the folder ContentArchiveWriter wrote. Returns the folder the player gets.
        public static string Write(ContentIndex index, string archive, bool isFetched, bool isIndexBrotli)
        {
            if (isFetched == false)
            {
                if (isIndexBrotli)
                    throw new BuildFailedException($"[ContentBuild] A brotli index of {index.Name} for a directory read in place");

                File.WriteAllText(Path.Combine(archive, ContentPaths.IndexFile(index.ManifestHash)), index.ToJson());
                return archive;
            }

            var folder = $"{Output}/{index.Name}";

            if (Directory.Exists(folder))
                Directory.Delete(folder, true);

            Directory.CreateDirectory(folder);
            File.Copy(Path.Combine(archive, ContentPaths.ArchiveFile), Path.Combine(folder, ContentPaths.ShippedArchive(index.ManifestHash)));

            var indexPath = Path.Combine(folder, ContentPaths.IndexFile(index.ManifestHash));

            if (isIndexBrotli)
                ContentBrotli.Write(indexPath + ContentPaths.BrotliExtension, index.ToJson());
            else
                File.WriteAllText(indexPath, index.ToJson());

            return folder;
        }
    }
}
