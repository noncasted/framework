using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;

namespace Internal
{
    // The files of a content directory a WebGL player with brotli compression fetches: <name>.br of every file of the
    // index in Library/ContentBrotli/<name>, what the player gets as StreamingAssets/Content/<name> instead of the raw
    // folder. The server sends them with Content-Encoding: br as Build/*.br of Unity (the same rules), so the browser
    // decodes them and the Cache API keeps the real bytes. A preloaded directory goes into the .data file, which the build
    // compresses whole; a standalone player and the other compression formats get the raw files.
    public static class ContentBrotli
    {
        public const string Output = "Library/ContentBrotli";

        public static bool IsShipped(BuildTarget target, bool isPreloaded)
        {
            if (target != BuildTarget.WebGL || isPreloaded)
                return false;

            if (PlayerSettings.WebGL.compressionFormat != WebGLCompressionFormat.Brotli)
                return false;

            // The loader decompresses its own files in JavaScript, the fetches of the content get the raw .br bytes.
            if (PlayerSettings.WebGL.decompressionFallback)
                throw new BuildFailedException("[ContentBuild] A fetched content directory does not work with the decompression fallback of " +
                                               "WebGL: turn it off and let the server send Content-Encoding: br, or preload the directory");

            return true;
        }

        // Compresses every file of the index from the raw folder into Output/<name>, anew on every build. Returns the folder.
        public static string Write(ContentIndex index, string source)
        {
            var folder = $"{Output}/{index.Name}";

            if (Directory.Exists(folder))
                Directory.Delete(folder, true);

            Directory.CreateDirectory(folder);

            // The biggest first: they take longest at quality 11.
            var files = index.Files.OrderByDescending(file => file.Size).ToArray();

            Parallel.ForEach(files, file =>
            {
                using var input = File.OpenRead(Path.Combine(source, file.Name));
                using var brotli = BrotliStreams.Compress(File.Create(Path.Combine(folder, file.Name + ContentPaths.BrotliExtension)));
                input.CopyTo(brotli);
            });

            return folder;
        }

        // The bytes the player downloads: the files of the index in the shipped folder.
        public static long ShippedBytes(ContentIndex index, string folder)
        {
            return index.Files.Sum(file =>
            {
                var path = Path.Combine(folder, file.Name + index.ShippedSuffix);

                if (File.Exists(path) == false)
                    throw new BuildFailedException($"[ContentBuild] The content directory {index.Name} ships no {path}");

                return new FileInfo(path).Length;
            });
        }
    }
}
