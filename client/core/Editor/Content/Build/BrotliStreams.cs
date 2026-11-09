using System;
using System.IO;
using System.IO.Compression;

namespace Internal
{
    // Brotli streams for the editor. The editor runs on .NET, where BrotliStream lives in System.IO.Compression.Brotli,
    // while the scripts compile against netstandard, which puts it in System.IO.Compression: a direct use does not load.
    // SmallestSize (quality 11, as the brotli of a WebGL build) is missing from netstandard too.
    public static class BrotliStreams
    {
        private static readonly Lazy<Type> StreamType = new(() =>
            Type.GetType("System.IO.Compression.BrotliStream, System.IO.Compression.Brotli", true));

        private const CompressionLevel SmallestSize = (CompressionLevel)3;

        // Writes compressed into the target; closing the stream closes the target.
        public static Stream Compress(Stream target)
        {
            return (Stream)Activator.CreateInstance(StreamType.Value, target, SmallestSize, false);
        }

        // Reads the source decompressed; closing the stream closes the source.
        public static Stream Decompress(Stream source)
        {
            return (Stream)Activator.CreateInstance(StreamType.Value, source, CompressionMode.Decompress, false);
        }
    }
}
