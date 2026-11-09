using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;

namespace Internal
{
    // The index of a content directory a WebGL player with brotli compression fetches is shipped as <hash>.index.json.br:
    // the server sends it with Content-Encoding: br as Build/*.br of Unity (the same rules), so the browser decodes it and
    // the Cache API keeps the json. The archive itself is never compressed again: its blocks are LZ4 and a Range request
    // needs its bytes as they are. A preloaded directory, a standalone player and the other formats get the raw index.
    public static class ContentBrotli
    {
        public static bool IsShipped(BuildTarget target, bool isPreloaded)
        {
            if (target != BuildTarget.WebGL || isPreloaded)
                return false;

            if (PlayerSettings.WebGL.compressionFormat != WebGLCompressionFormat.Brotli)
                return false;

            // The loader decompresses its own files in JavaScript, the fetch of the index gets the raw .br bytes.
            if (PlayerSettings.WebGL.decompressionFallback)
                throw new BuildFailedException("[ContentBuild] A fetched content directory does not work with the decompression fallback of " +
                                               "WebGL: turn it off and let the server send Content-Encoding: br, or preload the directory");

            return true;
        }

        public static void Write(string path, string text)
        {
            using var brotli = BrotliStreams.Compress(File.Create(path));
            var bytes = Encoding.UTF8.GetBytes(text);
            brotli.Write(bytes, 0, bytes.Length);
        }
    }
}
