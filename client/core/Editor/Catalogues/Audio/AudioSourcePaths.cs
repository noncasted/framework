using System;
using System.IO;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    [NoAutoStaticsCleanup]
    internal static class AudioSourcePaths
    {
        // Formats that Unity imports as AudioClip.
        private static readonly string[] SourceExtensions =
        {
            ".wav",
            ".mp3",
            ".ogg",
            ".aif",
            ".aiff",
            ".flac",
            ".mod",
            ".it",
            ".s3m",
            ".xm"
        };

        internal static bool IsSourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (IsEncodedPath(path))
                return true;

            var extension = Path.GetExtension(path);

            foreach (var sourceExtension in SourceExtensions)
            {
                if (extension.Equals(sourceExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // A clip shipped as ready mp3 bytes (EncodedAudio): Unity does not re-encode it to AAC for WebGL.
        internal static bool IsEncodedPath(string path)
        {
            return path.EndsWith(EncodedAudioDecoder.Extension, StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetSourceName(string path)
        {
            var fileName = Path.GetFileName(path);

            return IsEncodedPath(path)
                ? fileName.Substring(0, fileName.Length - EncodedAudioDecoder.Extension.Length)
                : Path.GetFileNameWithoutExtension(fileName);
        }
    }
}
