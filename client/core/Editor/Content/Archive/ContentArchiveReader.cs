using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Internal
{
    // Reads the table of contents of an archive of a content directory (UnityFS, the format of the engine since 5.x, as
    // ContentBuildInterface.ArchiveAndCompress writes it; layout after UnityDataTools): the spans of its files. The engine
    // does not promise the format, so anything it does not expect throws: the version, a table of contents that is not
    // LZ4 or at the end, a block shared by two files (every file of ArchiveAndCompress starts a block of its own).
    public static class ContentArchiveReader
    {
        private const string Signature = "UnityFS";
        private const uint SupportedVersion = 9;
        private const uint CompressionMask = 0x3f;
        private const uint InfoAtEnd = 0x80;
        private const uint BlockInfoPadding = 0x200;
        private const int Lz4 = 2;
        private const int Lz4HC = 3;
        private const int Alignment = 16;

        public static ContentArchiveLayout Read(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (ReadString(reader) != Signature)
                throw new InvalidDataException($"{path} is not a UnityFS archive");

            var version = ReadUInt(reader);

            if (version != SupportedVersion)
                throw new InvalidDataException($"The archive {path} has the version {version}, only {SupportedVersion} is known");

            ReadString(reader);
            ReadString(reader);

            var size = ReadLong(reader);
            var compressedInfo = ReadUInt(reader);
            var info = ReadUInt(reader);
            var flags = ReadUInt(reader);

            if (size != stream.Length)
                throw new InvalidDataException($"The archive {path} says it has {size} bytes, the file has {stream.Length}");

            if ((flags & InfoAtEnd) != 0)
                throw new InvalidDataException($"The table of contents of the archive {path} is at its end, the mount would read the whole file");

            var compression = (int)(flags & CompressionMask);

            if (compression != Lz4 && compression != Lz4HC)
                throw new InvalidDataException($"The table of contents of the archive {path} has the compression {compression}, not LZ4");

            Align(stream);
            var table = Lz4Block.Decode(reader.ReadBytes((int)compressedInfo), (int)info);

            if ((flags & BlockInfoPadding) != 0)
                Align(stream);

            return ReadTable(path, table, stream.Position, stream.Length);
        }

        private static ContentArchiveLayout ReadTable(string path, byte[] table, long dataStart, long length)
        {
            using var reader = new BinaryReader(new MemoryStream(table));

            // The hash of the uncompressed data.
            reader.ReadBytes(16);

            var blockCount = ReadInt(reader);
            var blocks = new (long Start, long End, long Offset, long Length)[blockCount];
            var position = 0L;

            for (var i = 0; i < blockCount; i++)
            {
                var uncompressed = ReadUInt(reader);
                var compressed = ReadUInt(reader);
                ReadUShort(reader);
                var offset = dataStart + ReadLong(reader);

                if (offset + compressed > length)
                    throw new InvalidDataException($"The block {i} of the archive {path} ends after the file");

                blocks[i] = (position, position + uncompressed, offset, compressed);
                position += uncompressed;
            }

            var nodeCount = ReadInt(reader);
            var files = new Dictionary<string, ContentArchiveSpan>(nodeCount);
            var block = 0;

            for (var i = 0; i < nodeCount; i++)
            {
                var start = ReadLong(reader);
                var end = start + ReadLong(reader);
                ReadUInt(reader);
                var name = ReadString(reader);

                files.Add(name, Span(path, name, blocks, ref block, start, end, dataStart));
            }

            return new ContentArchiveLayout(length, dataStart, files);
        }

        // The blocks of a file, in the order of the files: they start at its start and end at its end.
        private static ContentArchiveSpan Span(string path, string name, (long Start, long End, long Offset, long Length)[] blocks, ref int block,
            long start, long end, long dataStart)
        {
            if (start == end)
                return new ContentArchiveSpan(dataStart, 0);

            if (block >= blocks.Length || blocks[block].Start != start)
                throw new InvalidDataException($"The file {name} of the archive {path} does not start a block: its bytes are not its own");

            var first = blocks[block];
            var last = first;

            while (last.End < end)
            {
                block++;

                if (block >= blocks.Length)
                    throw new InvalidDataException($"The file {name} of the archive {path} ends after the last block");

                last = blocks[block];

                // Blocks start at 16 bytes: the padding between two blocks of a file is in its span.
                var gap = last.Offset - (blocks[block - 1].Offset + blocks[block - 1].Length);

                if (gap < 0 || gap >= Alignment)
                    throw new InvalidDataException($"The blocks of the file {name} of the archive {path} are not contiguous");
            }

            if (last.End != end)
                throw new InvalidDataException($"The file {name} of the archive {path} does not end a block: its bytes are not its own");

            block++;
            return new ContentArchiveSpan(first.Offset, last.Offset + last.Length - first.Offset);
        }

        private static void Align(Stream stream)
        {
            stream.Position = (stream.Position + Alignment - 1) / Alignment * Alignment;
        }

        private static string ReadString(BinaryReader reader)
        {
            var bytes = new List<byte>();

            for (var next = reader.ReadByte(); next != 0; next = reader.ReadByte())
                bytes.Add(next);

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static ushort ReadUShort(BinaryReader reader) => (ushort)BigEndian(reader, 2);
        private static uint ReadUInt(BinaryReader reader) => (uint)BigEndian(reader, 4);
        private static int ReadInt(BinaryReader reader) => checked((int)ReadUInt(reader));
        private static long ReadLong(BinaryReader reader) => checked((long)BigEndian(reader, 8));

        private static ulong BigEndian(BinaryReader reader, int count)
        {
            var bytes = reader.ReadBytes(count);

            if (bytes.Length != count)
                throw new EndOfStreamException();

            var value = 0UL;

            foreach (var next in bytes)
                value = (value << 8) | next;

            return value;
        }
    }
}
